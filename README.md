# Datafordelen
This is an event streaming application which retrieves the data from the Danish API called Datafordeler and process the information using kafka


## Requirements
* Docker
* In order to get acces of the necessary data an account on their website must first be created, this can be found at the following link:https://selfservice.datafordeler.dk/
The system uses two of their registers:
1. DAR, which holds the latest data for the adresses in Denmark 
2. GeoDanmarkVektor which holds the geographical data of Denmark.
<br>
More informations about the register structure and how to subscribe to them can be found on their website:https://datafordeler.dk/

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
