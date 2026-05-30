using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ClashXW.Native;

namespace ClashXW.Services
{
    public class ClashProcessService : IDisposable
    {
        private Process? _clashProcess;
        private readonly string _executablePath;
        private IntPtr _jobHandle;

        public ClashProcessService(string executablePath)
        {
            _executablePath = executablePath;
        }

        public void Start(string configPath)
        {
            if (string.IsNullOrEmpty(_executablePath) || !File.Exists(_executablePath))
            {
                throw new FileNotFoundException($"Clash executable not found at: {_executablePath}");
            }

            try
            {
                var assetsDir = Path.GetDirectoryName(_executablePath);
                var startInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = $"-d \"{assetsDir}\" -f \"{configPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Add config directory to SAFE_PATHS so Clash accepts config files from there
                var existingSafePaths = Environment.GetEnvironmentVariable("SAFE_PATHS") ?? "";
                var configDir = ConfigManager.ConfigDir;
                var safePaths = string.IsNullOrEmpty(existingSafePaths)
                    ? configDir
                    : $"{existingSafePaths},{configDir}";
                startInfo.Environment["SAFE_PATHS"] = safePaths;

                _clashProcess = new Process { StartInfo = startInfo };
                _clashProcess.Start();
                AssignToLifetimeJob(_clashProcess);
            }
            catch (Exception ex)
            {
                CleanupFailedStart();
                throw new InvalidOperationException($"Failed to start Clash process: {ex.Message}", ex);
            }
        }

        public void Stop()
        {
            ReleaseLifetimeJob();

            if (_clashProcess != null && !_clashProcess.HasExited)
            {
                KillProcessBestEffort(_clashProcess, "stop");
            }
        }

        public bool IsRunning => _clashProcess != null && !_clashProcess.HasExited;

        public void Dispose()
        {
            Stop();
            _clashProcess?.Dispose();
        }

        private void AssignToLifetimeJob(Process process)
        {
            var jobHandle = EnsureLifetimeJob();
            if (!NativeMethods.AssignProcessToJobObject(jobHandle, process.Handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "AssignProcessToJobObject failed");
            }

            Logger.Info($"Assigned Clash core PID={process.Id} to lifetime job");
        }

        private IntPtr EnsureLifetimeJob()
        {
            if (_jobHandle != IntPtr.Zero)
            {
                return _jobHandle;
            }

            var jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (jobHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObject failed");
            }

            var limits = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new NativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            var size = Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var limitsPtr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(limits, limitsPtr, false);
                if (!NativeMethods.SetInformationJobObject(
                        jobHandle,
                        NativeMethods.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                        limitsPtr,
                        (uint)size))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject failed");
                }
            }
            catch
            {
                NativeMethods.CloseHandle(jobHandle);
                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(limitsPtr);
            }

            _jobHandle = jobHandle;
            return _jobHandle;
        }

        private void CleanupFailedStart()
        {
            ReleaseLifetimeJob();

            if (_clashProcess != null)
            {
                KillProcessBestEffort(_clashProcess, "startup rollback");
                _clashProcess.Dispose();
                _clashProcess = null;
            }
        }

        private void KillProcessBestEffort(Process process, string context)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to kill Clash core during {context}: {ex.Message}");
            }
        }

        private void ReleaseLifetimeJob()
        {
            if (_jobHandle == IntPtr.Zero)
            {
                return;
            }

            if (!NativeMethods.CloseHandle(_jobHandle))
            {
                Logger.Warn($"Failed to close Clash lifetime job handle: {Marshal.GetLastWin32Error()}");
            }

            _jobHandle = IntPtr.Zero;
        }
    }
}
