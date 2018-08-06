# QBit Ninja

## An Open Source and powerful blockchain API
Showcase
-------
You can see the API documentation on [api.qbit.ninja](http://api.qbit.ninja/) and on [apiary](http://docs.qbitninja.apiary.io/).

You can try the API in .NET with the [nuget package](http://www.nuget.org/packages/QBitninja.Client).

Public servers : 
* Mainnet : [api.qbit.ninja](http://api.qbit.ninja/)
* Testnet : [tapi.qbit.ninja](http://tapi.qbit.ninja/)

## How to setup your own?

### Pre-Requisite

* Download and install [.NET Framework 7.2 Dev Pack](https://www.microsoft.com/net/download/thank-you/net472-developer-pack).
* Download and install [Visual studio 2017](https://visualstudio.microsoft.com/downloads/). You need to enable .NET Development and ASP.NET Web development.
* Download and install [Bitcoin Core 0.16.2](https://bitcoincore.org/bin/bitcoin-core-0.16.2/bitcoin-0.16.2-win64-setup.exe), and wait it is synchronized.
* Get a Microsoft Azure account.

In Azure, create one App resource group then you need to create:

* Storage resource
* Azure Bus resource
* Web App resource,
* Azure VM with 1 data disk of 1 TB attachd to it. (I advise you `D1 v2`)

### Setup the indexer

The indexer is the application which will listener your full node and index everything into your `Azure Storage`.
You can run it through the `QBitNinja.Listener.Console` project.

Assuming your Bitcoin node is fully synched,

```bash
git clone https://github.com/MetacoSA/QBitNinja/
```

Then edit `QBitNinja.Listener.Console/App.config`.

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

By careful：You need to compile QBitNinja in (preferably in Release mode) for the configuration to be effective, because QBitNinja will ultimately use the `QBitNinja.Listener.Console.exe.config` file which is in the same folder as `QBitNinja.Listener.Console.exe` for its configuration.

One you have setup everything, build `QBitNinja.Listener.Console` in `Release` mode and run `QBitNinja.Listener.Console.exe --init`.

You can repeat the same operation on multiple machine to index faster.

Once it finished, run `QBitNinja.Listener.Console.exe`.

We advise you to the Windows Task Scheduler to run `QBitNinja.Listener.Console.exe` and `bitcoind.exe` automatically even when the user is not logged on or when the virtual machine reboot.

### Setup the front

The front is a web application which will query your `Azure Storage` for blocks/transactions/balances indexed by the indexer.
You can run find it in the `QBitNinja` project.

The easiest is deploy via `Visual Studio 2017`.

* Download your Web App profile by going into your Web App resource settings in Azure, and clicking on `Get publish profile`
* Open the solution under `Visual Studio 2017`
* Setup the `Web.config` exactly how you set up the `App.config` in the previous step
* Right click on the `QBitNinja` project and click on `Publish`.
* Click on `New profile...`
* In the new window, click on the bottom left `Import Profiles` and select your downloaded publish profile
* Click on `Publish`

## How to build?

### Via visual studio (recommended)

* Download and install [Visual studio 2017](https://visualstudio.microsoft.com/downloads/). (You need to enable .NET Development and ASP.NET Web development)
* Download and install [.NET Framework 7.2 Dev Pack](https://www.microsoft.com/net/download/thank-you/net472-developer-pack).

Open the solution and build.

### By command line

* Download and install [Build Tools for Visual studio 2017](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2017).

Then use `msbuild.exe`:
```powershell
msbuild.exe /restore
msbuild.exe /p:Configuration=Release
```

Configuration file example
==========


Unity
==========
In order for the API to work in Unity with .NET 4.6 for Android devices you should:

* `QBitNinjaClient.SetCompression(false);` Because it's missing the DLL MonoPosixHelper from the build
* `QBitNinjaClient client = new QBitNinjaClient("http://api.qbit.ninja/", NBitcoin.Network.Main);` because HTTPS with `HttpClient` seems to not work correctly.
* Scripting Runtime Version: Select "Experimental (.NET 4.6 Equivalent)"

License
-------
This project is released under the terms of the GPLv3 license. See [LICENSE](LICENSE) for more information or see http://opensource.org/licenses/GPL-3.0.
