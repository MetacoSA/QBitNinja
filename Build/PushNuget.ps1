C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe "Build.csproj" /p:Configuration=Release

cd ..\QBitNinja.Client.NETCore
dotnet restore
dotnet build -c Release
cd ..\Build
del "*.nupkg"

C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild.exe "PushNuget.csproj" /p:Configuration=Release

#.\GitLink.exe ".." -ignore "QBitNinja.Tests,Build,QBitNinja.Client.Tests,QBitNinja.Hosting.Azure.Web,Common"

..\.Nuget\nuget pack "..\QBitNinja.Client/QBitNinja.Client.nuspec"
..\.Nuget\nuget pack "..\QBitNinja/QBitNinja.nuspec"

#forfiles /m *.nupkg /c "cmd /c NuGet.exe push @FILE -source https://api.nuget.org/v3/index.json"
#(((dir *.nupkg).Name)[0] -match "[0-9]+?\.[0-9]+?\.[0-9]+?\.[0-9]+")
#$ver = $Matches.Item(0)
#git tag -a "v$ver" -m "$ver"
#git push --tags
