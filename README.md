# GraphiteExportandForwarder
Recieves metrics from an Graphite API and send them to another Graphite API

Copy your existing metrics from one instance to another. This is helpful if you want to change the storage backend of Graphite, the whole stack or move to another server.

## How does it work
### Export
The tool uses the graphite [HTTP API](https://graphite-api.readthedocs.io/en/latest/api.html) for exporting the metrics, so every stack which uses a Graphite compatible API is supported as source. 

### Forward
The metrics are send with the [plaintext protocol](https://graphite.readthedocs.io/en/latest/feeding-carbon.html#the-plaintext-protocol) to a carbon compatible API. 

### New metrics
New metrics that are send during the run of GraphiteExportandForwarder must be handled with other technics like carbon-relay-ng which is able to send the metrics two multiple graphite instances. 


## Configuration
Change the values of GraphiteExportandForwarder.exe.config. Following settings are possible:

- maxThreads: Integer value,  Defines how many threads are used in parallel to query and export metrics. Keep it on a small value < 10, Default: 2
- startmetric: Integer value, you can define to skip the first e.g. 100 metrics, Default: 0
- printStatusEveryXSeconds: Integer value, print out the status every x seconds, Default: 10
- GraphiteSourceServer: Defines the Source Server Graphite API, Value: https://[servername]:[Port]/
- GraphiteDestinationServer: Servername of the destination Graphite server (Carbon compatible): Value: [servername]
- GraphiteSourceUsername: Username, if needed for Basic HTTP Authentication at source graphite API, Empty for no authentication
- GraphiteSourcePassword: Password, if needed for Basic HTTP Authentication at source graphite API
- DoNotValidateSSLCert: Ignore self signed and invalid SSL certs on the source graphite server, Default: true
- ImportDataFromDateon: Specifed a date (YYYYMMDD) from which on the data should be queried on the source. Default: 20150101
- OnlyImportMetricsThatStartsWith: Possibility to filter metrics that should be exported. Default: [empty]


## System Requierments
- .NET 4.5 or greater on Windows

Mono is not tested.


## Dependencies for reference
Following projects are used in the source code:

- [ragnard/Graphite.NET](https://github.com/ragnard/Graphite.NET) 
- [JamesNK/Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)

Thank you for the great work.
