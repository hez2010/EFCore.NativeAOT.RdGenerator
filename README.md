# EFCore Runtime Directives Generator for NativeAOT

This project indents to generate all necessary runtime directives for EFCore in NativeAOT scenario.

Status: WIP.

## Build
```bash
dotnet build -c Release
```

## Run
```bash
dotnet run -c Release <your-assembly> <dbcontext...>
```

Example: 
```bash
dotnet run -c Release Foo.dll Foo.MyDbContext1 Foo.MyDbContext2 Foo.MyDbContext3
```