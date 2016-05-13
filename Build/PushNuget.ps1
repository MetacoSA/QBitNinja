del "*.nupkg"
impvs
msbuild.exe "Build.csproj" /p:Configuration=Release
msbuild.exe "PushNuget.csproj" /p:Configuration=Release

.\GitLink.exe ".." -ignore "QBitNinja.Tests,Build,QBitNinja.Client.Tests,QBitNinja.Hosting.Azure.Web,Common"

nuget pack "../QBitNinja.Client/QBitNinja.Client.nuspec"
nuget pack "../QBitNinja/QBitNinja.nuspec"

forfiles /m *.nupkg /c "cmd /c C:/ProgF/NuGet/nuget.exe push @FILE"
(((dir *.nupkg).Name)[0] -match "[0-9]+?\.[0-9]+?\.[0-9]+?\.[0-9]+")
$ver = $Matches.Item(0)
git tag -a "v$ver" -m "$ver"
git push --tags
