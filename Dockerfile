FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json EducationKeeptrack.slnx ./
COPY src ./src
RUN dotnet publish src/Host -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=build /app ./
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN dotnet Host.dll --install-report-browser && /app/.playwright/node/linux-x64/node /app/.playwright/package/cli.js install-deps chromium
RUN mkdir /keys && chown $APP_UID /keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Host.dll"]
