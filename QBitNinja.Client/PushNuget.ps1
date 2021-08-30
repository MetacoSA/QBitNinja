rm "bin\release\" -Recurse -Force
dotnet build /p:Configuration=Release /p:LIB_ONLY=true
dotnet pack --no-build --no-restore --configuration Release
dotnet nuget push "bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"
$ver = ((ls .\bin\release\*.nupkg -File)[0].Name -replace '([^\.\d]*\.)+(\d+(\.\d+){1,3}).nupkg', '$2')
git tag -a "Client/v$ver" -m "Client/$ver"
git push --tags
