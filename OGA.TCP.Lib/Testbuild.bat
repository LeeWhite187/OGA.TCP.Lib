REM NET Core Software Library

REM Build the library...
dotnet restore --interactive "./OGA.TCP.Lib_NETStd21/OGA.TCP.Lib_NETStd21.csproj"
REM dotnet build "./OGA.Common.Lib_NET48/OGA.Common.Lib_NET48.csproj" -c Debug --runtime win-x64 --no-self-contained
REM dotnet build "./OGA.Common.Lib_NET48/OGA.Common.Lib_NET48.csproj" -c Debug --runtime win-x86 --no-self-contained
dotnet build "./OGA.TCP.Lib_NETStd21/OGA.TCP.Lib_NETStd21.csproj" -c DebugLinux --runtime linux --no-self-contained

dotnet build "./OGA.TCP.Lib_NETStd21/OGA.TCP.Lib_NETStd21.csproj" -c DebugWin --runtime win --no-self-contained

