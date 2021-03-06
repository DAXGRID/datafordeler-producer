FROM mcr.microsoft.com/dotnet/sdk:5.0-focal  AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY ./src/*.csproj ./
RUN dotnet restore

#Copy the convert script
COPY ./src/convert_script.sh ./out/datafordeleren/


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

