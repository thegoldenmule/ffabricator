VERSION=0.1.0

build:
	docker run --rm -v $(shell pwd):/app -w /app mcr.microsoft.com/dotnet/sdk:5.0 dotnet publish -c release -o out
	docker build -t thegoldenmule/ffab:${VERSION} -f Dockerfile .