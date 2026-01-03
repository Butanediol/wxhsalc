# wxhsalc

<img width="435" height="255" alt="image" src="https://github.com/user-attachments/assets/fc35cdfb-dbcc-4797-b95b-171932a7ed7b" />

## Why

1. I believe proxy experience should be as transparent as possible, a clunky GUI really doesn't feel like so.
2. Most GUI clients tend to mess up my config file, they don't fully respect my clash config.
3. Most GUI clients install a service to avoid UAC prompts, convenient but I really don't like having another third-party service running background.
4. Most GUI clients are ugly (sorry). They don't respect platform native design language.
5. The old [ClashXW](https://github.com/ysc3839/ClashXW) is great, big shoutout to the original author, but it doesn't have a TUN toggle button, nor `hidden` property support. (If you don't need these features, it might be better to just use the original ClashXW)

## This is not for you if

1. You don't know what Clash/Mihomo is.
2. You don't know how to use Clash/Mihomo **core**.
3. You want a fancy GUI client.
4. You want minimal performance overhead, I am a shit coder.

## How to use

1. Download the latest release from the [Releases](https://github.com/Butanediol/Clash/releases) page.
2. Extract the downloaded archive to a folder of your choice.
3. Run the `ClashXW.exe` file.
4. Edit the configuration file under `%APPDATA%\ClashXW\config`. You can have multiple config files there.
5. Reload the application to apply the changes.

## Bonus

- If you want to use TUN mode, you need to run the program with Administrator privileges.
- You don't need to wait for this application to update if you want to use a newer version of clash. You can simply replace the `clash.exe` file in the installation directory with the newer version. Or open the dashboard, go to Settings and click **Upgrade Clash Core**.

## Development

### Prerequisites

-   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Building

To build the project for debugging:

```sh
dotnet build
```

### Publishing

To create a release build:

```sh
dotnet publish --configuration Release
```

The self-contained application will be located in the `bin/Release/net8.0-windows/publish/` directory.