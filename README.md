# CometD for Salesforce Platform events
[![Build status](https://ci.appveyor.com/api/projects/status/6t0kmjpr6ckvhrxe?svg=true)](https://ci.appveyor.com/project/kdcllc/cometd-netcore)

This repo contains the CometD .NET Core implementation of the Java ported code.
1. CometD.NetCore - CometD.org implementation


## Nuget Packages
1. ``` Install-Package CometD.NetCore2 -Version 1.0.0 ```

## Saleforce
[Video](https://www.youtube.com/watch?v=L6OWyCfQD6U)
1. Sing up for development sandbox with Saleforce: [https://developer.salesforce.com/signup](https://developer.salesforce.com/signup)
2. Create Connected App in Salesforce
3. Create a Platform Event

### Create Connected App in Salesforce
1. Setup -> Quick Find -> manage -> App Manager -> New Connected App.
2. Basic Info:

![info](./img/new-app-basic-info.jpg)

3. API (Enable OAuth Settings):
![settings](./img/new-app-api-auth.jpg)

4. Retrieve `Consumer Key` and `Consumer Secret` to be used within the Test App

### Create a Platform Event
1. Setup -> Quick Find -> Events -> Platform Events -> New Platform Event:

![event](./img/new-platform-event.jpg)

2. Add Custom Field

![event](./img/new-platform-event-field.jpg)

(note: use sandbox custom domain for the login to workbench in order to install this app within your production)

Use workbench to test the Event [workbench](https://workbench.developerforce.com/login.php?startUrl=%2Finsert.php)


## Special thanks to the following projects and contributors:
- [Oyatel/CometD.NET](https://github.com/Oyatel/CometD.NET)
- [nthachus/CometD.NET](https://github.com/nthachus/CometD.NET)
- [tdawgy/CometD.NetCore](https://github.com/tdawgy/CometD.NetCore)
- [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)
- [Chris Woolum](https://github.com/cwoolum)
