FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore the existing solution with cacheable project files.
COPY IMS.sln ./
COPY src/IMS.API/IMS.API.csproj src/IMS.API/
COPY src/IMS.Application/IMS.Application.csproj src/IMS.Application/
COPY src/IMS.Domain/IMS.Domain.csproj src/IMS.Domain/
COPY src/IMS.Infrastructure/IMS.Infrastructure.csproj src/IMS.Infrastructure/
COPY tests/IMS.Tests/IMS.Tests.csproj tests/IMS.Tests/
RUN dotnet restore IMS.sln

COPY src/ src/
RUN dotnet publish src/IMS.API/IMS.API.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "IMS.API.dll"]