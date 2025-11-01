# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./ConfigCase.sln ./
COPY ./src/Config.Core/ ./src/Config.Core/
COPY ./src/Sample.ServiceA/ ./src/Sample.ServiceA/
RUN dotnet restore
RUN dotnet publish src/Sample.ServiceA/Sample.ServiceA.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet","Sample.ServiceA.dll"]
