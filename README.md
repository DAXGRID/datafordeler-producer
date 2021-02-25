# Datafordelen
This is an event streaming application which retrieves the data from the Danish platform called Datafordeler and process the information using kafka


## Requirements
* Docker
* The system uses two of their registers:
1. DAR, which holds the latest data for the adresses in Denmark 
2. GeoDanmarkVektor which holds the geographical data of Denmark.
3. BBR, which holds information about buildings in Denmark.
<br>
Information on how to set up the registers can be found in the following section.

## Datafordeler


## Configure environment variable
```
. ./dev/docker-compose.yml
```
## Building
```
docker build -t datafordeleren
```

## Running
```
docker-compose up
```
