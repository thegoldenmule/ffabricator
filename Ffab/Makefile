VERSION=0.1.0

build:
	docker run --rm -v $(shell pwd):/app -w /app mcr.microsoft.com/dotnet/sdk:5.0 dotnet publish -c release -o out
	cp config.akka out
	docker build -t thegoldenmule/ffab:${VERSION} -f Dockerfile .

run:
	mkdir -p $(shell pwd)/working/Input
	mkdir -p $(shell pwd)/working/Output
	docker run \
		-v $(shell pwd)/creds.json:/App/creds.json \
		-v $(shell pwd)/working/Input:/App/Input \
		-v $(shell pwd)/working/Output:/App/Output \
		-p 10104:10104 \
		thegoldenmule/ffab:${VERSION}