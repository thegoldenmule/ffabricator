FROM mcr.microsoft.com/dotnet/aspnet:5.0

RUN apt-get update && apt-get install -y ffmpeg

COPY out/ App/
WORKDIR /App
ENV GOOGLE_APPLICATION_CREDENTIALS=creds.json
ENTRYPOINT ["dotnet", "Ffab.dll", "-a", "config.akka"]

