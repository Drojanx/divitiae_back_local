
# Divitiae Backend

Para instalar localmente, simplemente clonad este repositorio

````
git clone https://github.com/Drojanx/divitiae_back_local
````
Luego, se lanzan las bases de datos. Primero SQL Server:
````
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=dIviti@3" -p 1433:1433 --name divitiaeSQLserver --hostname divitiaeSQLserver -d mcr.microsoft.com/mssql/server:2022-latest
````
Como ya tenemos la migración, podemos hacer simplemente esto para actualizar la base de datos:
````
dotnet ef database update
````
Ahora, la base de datos Mongodb:
````
docker run -d --name divitiaeMongo -e MONGO_INITDB_ROOT_USERNAME=mongoadmin -e MONGO_INITDB_ROOT_PASSWORD=dIviti@3 -p 27017:27017 mongo
````
Con esto y tal como está configurado el fichero appsettings.json, no habría nada más que hacer, sólo ejecutar la solución.