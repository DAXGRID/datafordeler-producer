#!/bin/bash
FILES=datafordeleren/geo/geogml/*.shp
for f in $FILES
do
  file=`basename "$f"`
  echo ${file%.*} 
  echo $f
  # take action on each file. $f store current file name
  ogr2ogr -f "GeoJSON" datafordeleren/geo/${file%.*}.json $f 
done
