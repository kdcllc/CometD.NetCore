# CometD for Salesforce Platform events

[![Build status](https://ci.appveyor.com/api/projects/status/6t0kmjpr6ckvhrxe?svg=true)](https://ci.appveyor.com/project/kdcllc/cometd-netcore)
[![NuGet](https://img.shields.io/nuget/v/CometD.NetCore.svg)](https://www.nuget.org/packages?q=CometD.NetCore)
[![MyGet](https://img.shields.io/myget/kdcllc/v/CometD.NetCore2.svg?label=myget)](https://www.myget.org/F/kdcllc/api/v2)

This repo contains the CometD .NET Core implementation of the Java ported code.

- CometD.NetCore - [CometD.org](CometD.org) implementation, supports replay id.
- [CometD.NetCore.Salesforce project](https://github.com/kdcllc/CometD.NetCore.Salesforce) - provides with implementation of this library.

## Install

```bash
    PM> Install-Package CometD.NetCore2
```

Or

```bash
    dotnet add package CometD.NetCore2
```

## Configure Saleforce Developer instance

[Watch: Salesforce Platform Events - Video](https://www.youtube.com/watch?v=L6OWyCfQD6U)

1. Sing up for development sandbox with Saleforce: [https://developer.salesforce.com/signup](https://developer.salesforce.com/signup).
2. Create Connected App in Salesforce.
3. Create a Platform Event.

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

## OAuth Apps

[Use login instead of test](https://github.com/developerforce/Force.com-Toolkit-for-NET/wiki/Web-Server-OAuth-Flow-Sample#am-i-using-the-test-environment)

## Special thanks to the following projects and contributors:

- [Oyatel/CometD.NET](https://github.com/Oyatel/CometD.NET)
- [nthachus/CometD.NET](https://github.com/nthachus/CometD.NET)
- [tdawgy/CometD.NetCore](https://github.com/tdawgy/CometD.NetCore)
- [anthonyreilly/NetCoreForce](https://github.com/anthonyreilly/NetCoreForce)
- [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)
- [Chris Woolum](https://github.com/cwoolum)
