# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./ConfigCase.sln ./
COPY ./src/Config.Core/ ./src/Config.Core/
COPY ./src/Config.WebAdmin/ ./src/Config.WebAdmin/
RUN dotnet restore
RUN dotnet publish src/Config.WebAdmin/Config.WebAdmin.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://0.0.0.0:5080
EXPOSE 5080
ENTRYPOINT ["dotnet","Config.WebAdmin.dll"]
