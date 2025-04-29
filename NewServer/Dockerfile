FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:1875fc5f4be6211c22c4353aafd6b13279c0175f277fbfa53d52375d99e8a9b8 AS build

WORKDIR /NewServer

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -o out

EXPOSE 4567

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:ab605d8d1b0886af2ef4bbaf5167fbe237670b6fd9829a05c4d08406afdd985e
WORKDIR /NewServer
COPY --from=build /NewServer/out .
ENTRYPOINT ["dotnet", "NewServer.dll"]