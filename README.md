# DbTestMonkey
DbTestMonkey is an open source integration test library designed to handle all the behind the scenes complexity of testing against databases.

Currently supported database providers:
- SQL Server 2012 and above.

Currently supported test frameworks:
- [XUnit](https://github.com/xunit/xunit)
- [NUnit](https://github.com/nunit/nunit)

[![Build status](https://ci.appveyor.com/api/projects/status/i5a8oawlq9udcgbl/branch/master?svg=true)](https://ci.appveyor.com/project/DbTestMonkey/dbtestmonkey/branch/master)

## Installing DbTestMonkey
First, [install NuGet](http://docs.nuget.org/docs/start-here/installing-nuget). 

Next, install [DbTestMonkey.Providers.SqlServer](https://www.nuget.org/packages/DbTestMonkey.Providers.SqlServer) from the Package Manager Console:

    PM> Install-Package DbTestMonkey.Providers.SqlServer

Finally, install [DbTestMonkey.XUnit.Fody](https://www.nuget.org/packages/DbTestMonkey.XUnit.Fody) from the Package Manager Console:

    PM> Install-Package DbTestMonkey.XUnit.Fody

## Getting Started
Information on getting started with DbTestMonkey can be found on the [DbTestMonkey Wiki](https://github.com/DbTestMonkey/DbTestMonkey/wiki).
