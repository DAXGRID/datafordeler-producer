FROM mcr.microsoft.com/dotnet/sdk:5.0-focal  AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY ./src/*.csproj ./
RUN dotnet restore

#Copy the convert script
COPY ./src/convert_script.sh ./out/datafordeleren/

# copy geodata files
#COPY ./src/geodanmark_60_nohist.bygning.json ./out/datafordeleren/
#COPY ./src/geodanmark_60_nohist.hegn.json ./out/datafordeleren
#COPY ./src/geodanmark_60_nohist.jernbane.json ./out/datafordeleren/
COPY ./src/geodanmark_60_nohist.soe.json ./out/datafordeleren/
#COPY ./src/geodanmark_60_nohist.vejkant.json ./out/datafordeleren/
COPY ./src/geodanmark_60_nohist.vejmidte.json ./out/datafordeleren/
#COPY ./src/bebyggelse.json ./out/datafordeleren/

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
WORKDIR /app
COPY --from=build-env /app/out .

RUN apt-get update && apt-get install -y \
  gdal-bin


ENTRYPOINT ["dotnet", "Datafordeleren.dll"]

