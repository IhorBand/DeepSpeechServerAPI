# DeepSpeechServerAPI

Docker Usage:
1. From repo root directory

docker build -f "DeepSpeechServerAPI\Dockerfile" -t deepspeechserverapi .

2. change {PathToRepoRootDirectory} with ABSOLUTE path to root directory of that repo and run

docker run -dt -e "DOTNET_USE_POLLING_FILE_WATCHER=1" -e "ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS=true" -e "ASPNETCORE_ENVIRONMENT=Development" -e "ASPNETCORE_URLS=https://+:443;http://+:80" -e "NUGET_PACKAGES=/root/.nuget/fallbackpackages" -e "NUGET_FALLBACK_PACKAGES=/root/.nuget/fallbackpackages" -p 2703:80 -p 2702:443 --name deepspeechserverapi deepspeechserverapi -f /dev/null

3. Try to open page https://localhost:8443/swagger(https://localhost:8443/stt if you used Release environment) or http://localhost:8080/swagger(http://localhost:8080/stt if you used Release environment)

