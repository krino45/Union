FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

COPY . .

RUN dotnet restore src/UniScheduler.Api/UniScheduler.Api.csproj

RUN dotnet publish src/UniScheduler.Api/UniScheduler.Api.csproj \
    -c Release \
    -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "UniScheduler.Api.dll"]