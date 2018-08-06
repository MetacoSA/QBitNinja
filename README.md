QBit Ninja
==========
**An Open Source and powerful blockchain API**
Showcase
-------
You can see the API documentation on [api.qbit.ninja](http://api.qbit.ninja/) and on [apiary](http://docs.qbitninja.apiary.io/).

You can try the API in .NET with the [nuget package](http://www.nuget.org/packages/QBitninja.Client).

Public servers : 
* Mainnet : [api.qbit.ninja](http://api.qbit.ninja/)
* Testnet : [tapi.qbit.ninja](http://tapi.qbit.ninja/)

How to setup your own?
==========

In Azure, create one App resource group then you need to create:

* Storage resource
* Azure Bus resource
* Web App resource,
* Azure VM with 1 data disk of 1 TB attachd to it. (I advise you an A5)

Download and install [.NET Framework 7.2 Dev Pack](https://www.microsoft.com/net/download/thank-you/net472-developer-pack).

Download and install [Visual studio 2017](https://visualstudio.microsoft.com/downloads/). You need to enable .NET Development and ASP.NET Web development.

Download and install [Bitcoin Core 0.16.2](https://bitcoincore.org/bin/bitcoin-core-0.16.2/bitcoin-0.16.2-win64-setup.exe), and wait it is synchronized.

You deploy the QBitNinja project in the Web App, you configure the appsettings to point to the Storage and AzureBus correctly.

On the VM, you setup a fully sync bitcoin node with the datadir in the data disk of 1TB, you then run `QBitNinja.Listener.Console --init` after having configured the `app.config` with same info as the web app, and wait for 1 week.

You can run this command line on several machine concurrently to speedup indexing.

Take a very good VM for the indexing. You can scale down once everything is synched.

Once everything is synched, run `QBitNinja.Listener.Console --Listen`.

If you want the listener to run even if the server reboot, use the Windows Task Scheduler to run the program even when the user is not logged on.

How to build?
==========

Ensure MSBuild version is a least  15.5:

```
msbuild.exe /restore
msbuild.exe /p:Configuration=Release
```

Or use Download and install [Visual studio 2017](https://visualstudio.microsoft.com/downloads/). (You need to enable .NET Development and ASP.NET Web development.)

Configuration file example
==========

Your `QBitNinja.Listener.Console` `app.config` file should looks like.

```
	<appSettings>
		<add key="Azure.AccountName" value="azurestorageaccountname" />
		<add key="Azure.Key" value="azurestoragekey" />
        <add key="RPCConnectionString" value="default"/>
		<add key="Bitcoin.Network" value="mainnet" />
		<add key="Node" value="127.0.0.1" />
		<add key="ServiceBus" value="Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mysecretkey" />
	</appSettings>
```

* `Azure.AccountName` and `Azure.Key` are in the Azure Portal, in the settings of your Azure Storage Resource,
* `Bitcoin.Network` can be `mainnet`, `testnet`, `regtest`.
* `Node` represented the P2P connection to your bitcoin node. Do not forget to whitelist the indexer in your node's settings (typically adding `whitelist=127.0.0.1` in `bitcoin.conf`)
* `ServiceBus` connection string to your Service Bus Namespace (in `Shared access policies` inside Azure)
* `RPCConnectionString` optional, but needed for more reliably broadcast transactions. 

Example of `RPCConnectionString`:

* `default`: Assume your are running `bitcoind` locally with default settings.
* `cookiefile=C:\path\to\.cookie`: If you run `bitcoind` in a different data directory with default authentication, you need to set the path to it.
* `server=http://127.0.0.1:29292`: If you run `bitcoind` RPC on a different port than default.
* `myuser:password`: If you run `bitcoind` with `rpcuser` and `rpcpassword`.
* `server=http://127.0.0.1:29292;myuser:password`: If you run `bitcoind` RPC with `rpcuser` and `rpcpassword`, on a different port than default.
* `server=http://127.0.0.1:29292;cookiefile=C:\path\to\.cookie`: If you run `bitcoind` RPC with `rpcuser` and `rpcpassword`, in a different data directory with default authentication.

Unity
==========
In order for the API to work in Unity with .NET 4.6 for Android devices you should:

* `QBitNinjaClient.SetCompression(false);` Because it's missing the DLL MonoPosixHelper from the build
* `QBitNinjaClient client = new QBitNinjaClient("http://api.qbit.ninja/", NBitcoin.Network.Main);` because HTTPS with `HttpClient` seems to not work correctly.
* Scripting Runtime Version: Select "Experimental (.NET 4.6 Equivalent)"

License
-------
This project is released under the terms of the GPLv3 license. See [LICENSE](LICENSE) for more information or see http://opensource.org/licenses/GPL-3.0.
