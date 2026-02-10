# Fast Deploy for Debug Builds

## Overview

Fast Deploy is an Android optimization feature that significantly reduces deployment time during debug builds by only pushing changed assemblies to the device instead of rebuilding and deploying the entire APK.

## Configuration

Fast Deploy is **enabled by default** for Debug builds in `PmSTools.csproj`:

```xml
<PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
  <!-- ... other properties ... -->
  <AndroidEnableFastDeployment>true</AndroidEnableFastDeployment>
  <EmbedAssembliesIntoApk>false</EmbedAssembliesIntoApk>
</PropertyGroup>
```

### Property Explanation

- **`AndroidEnableFastDeployment`** (true): Enables the fast-deploy mechanism for incremental assembly deployment
- **`EmbedAssembliesIntoApk`** (false): Prevents embedding assemblies into the APK, allowing them to be deployed separately for faster iteration

## Usage

### First Deploy (Initial Installation)
```bash
dotnet publish -c Debug -f net10.0-android -t:Install
```
This creates and installs the full APK on your Android device/emulator.

### Subsequent Deploys (Fast Deploy)
```bash
dotnet publish -c Debug -f net10.0-android -t:Install
```
On subsequent builds, only changed assemblies will be deployed, dramatically reducing deployment time.

### From Visual Studio / Rider
Simply build and deploy as normal using the IDE's debug tools. Fast Deploy is automatically applied for Debug configurations.

## Benefits

- ⚡ **Faster iteration**: 5-10x faster deployment for small code changes
- 🎯 **Preserves app state**: No need to fully reinstall the app between builds
- 💾 **Reduced bandwidth**: Only modified files are transferred
- 🔄 **Incremental builds**: Perfect for rapid development cycles

## Release Builds

Release builds do **not** use Fast Deploy. They deploy the complete signed APK for production readiness.

## Troubleshooting

### App crashes after fast-deploy
If the app crashes after deployment, perform a clean reinstall:
```bash
dotnet publish -c Debug -f net10.0-android -t:Uninstall
dotnet publish -c Debug -f net10.0-android -t:Install
```

### Fast Deploy not working
Ensure:
1. You're building in **Debug** configuration (not Release)
2. Your Android device/emulator has sufficient free storage
3. The app is already installed on the device from an earlier full deploy

### Device/Emulator Requirements
- Android 5.0+ (API level 21+)
- At least 100 MB free storage
- USB connection or emulator running

## More Information

- [Microsoft Docs: Android Fast Deployment](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment)
- [MAUI Android Deployment](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/overview)
