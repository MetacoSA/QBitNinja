del "*.nupkg"
impvs
msbuild.exe "Build.csproj" /p:Configuration=Release
msbuild.exe "PushNuget.csproj" /p:Configuration=Release

nuget pack "../RapidBase.Client/RapidBase.Client.nuspec"

forfiles /m *.nupkg /c "cmd /c C:/ProgF/NuGet/nuget.exe push @FILE"
(((dir *.nupkg).Name) -match "[0-9]+?\.[0-9]+?\.[0-9]+?\.[0-9]+")
$ver = $Matches.Item(0)
git tag -a "v$ver" -m "$ver"
git push --tags
