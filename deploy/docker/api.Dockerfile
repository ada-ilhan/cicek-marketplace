# Build context: backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Cicek.Api/Cicek.Api.csproj -c Release -o /app /p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Writable directory for the development image store (Media__Provider=Local); a named volume mounted here inherits
# this ownership. Production uses object storage instead.
RUN mkdir -p /data/media && chown $APP_UID /data/media
# Non-root user provided by the official .NET images.
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cicek.Api.dll"]
