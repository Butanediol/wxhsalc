using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ClashXW.Services
{
    public class ClashProcessService : IDisposable
    {
        private Process? _clashProcess;
        private readonly string _executablePath;
        private IntPtr _jobHandle = IntPtr.Zero;

        // Windows Job Object P/Invoke declarations
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoClass infoClass,
            IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private enum JobObjectInfoClass
        {
            JobObjectExtendedLimitInformation = 9
        }

        [Flags]
        private enum JobObjectLimitFlags : uint
        {
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public JobObjectLimitFlags LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

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

                // Create and configure job object to ensure child process exits with parent
                CreateAndAssignJobObject();
            }
            catch (Exception ex)
            {
                CleanupJobObject();  // Cleanup potentially created job object
                throw new InvalidOperationException($"Failed to start Clash process: {ex.Message}", ex);
            }
        }

        public void Stop()
        {
            if (_clashProcess != null && !_clashProcess.HasExited)
            {
                _clashProcess.Kill();
            }
        }

        private void CleanupJobObject()
        {
            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
            }
        }

        private bool CreateAndAssignJobObject()
        {
            try
            {
                // Create anonymous job object
                _jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero)
                {
                    Debug.WriteLine($"Failed to create job object. Error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                // Configure job object to kill all processes when the job is closed
                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JobObjectLimitFlags.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                    if (!SetInformationJobObject(_jobHandle, JobObjectInfoClass.JobObjectExtendedLimitInformation,
                        extendedInfoPtr, (uint)length))
                    {
                        Debug.WriteLine($"Failed to set job object information. Error: {Marshal.GetLastWin32Error()}");
                        CleanupJobObject();
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }

                // Assign process to job object
                if (_clashProcess != null && !AssignProcessToJobObject(_jobHandle, _clashProcess.Handle))
                {
                    Debug.WriteLine($"Failed to assign process to job object. Error: {Marshal.GetLastWin32Error()}");
                    CleanupJobObject();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception creating job object: {ex.Message}");
                CleanupJobObject();
                return false;
            }
        }

        public bool IsRunning => _clashProcess != null && !_clashProcess.HasExited;

        public void Dispose()
        {
            Stop();
            _clashProcess?.Dispose();
            CleanupJobObject();  // Cleanup job object handle
        }
    }
}
