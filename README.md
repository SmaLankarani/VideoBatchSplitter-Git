# Video Batch Splitter

A small Windows WPF application that randomly splits videos from one folder into separate batches.

## Features

- Select a source folder
- Detect common video formats
- Default batch size: 100 videos
- Randomize/shuffle files before splitting
- Copy mode: keeps originals
- Move mode: moves originals into batch folders
- Creates `Batch_001`, `Batch_002`, ...
- Progress bar and status
- Handles duplicate filenames

## Requirements

- Windows
- .NET 8 SDK

## Run

```powershell
dotnet restore
dotnet run
```

## Build an EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The executable will be under:

`bin\Release\net8.0-windows\win-x64\publish\`
