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

You deploy the QBitNinja project in the Web App, you configure the appsettings to point to the Storage and AzureBus correctly.

On the VM, you setup a fully sync bitcoin node with the datadir in the data disk of 1TB, you then run `QBitNinja.Listener.Console --init` after having configured the `app.config` with same info as the web app, and wait for 1 week.

You can run this command line on several machine concurrently to speedup indexing.

Take a very good VM for the indexing. You can scale down once everything is synched.

Once everything is synched, run `QBitNinja.Listener.Console --Listen`.

If you want the listener to run even if the server reboot, use the Windows Task Scheduler to run the program even when the user is not logged on.

Unity
==========
In order for the API to work in Unity with .NET 4.6 for Android devices you should:

* `QBitNinjaClient.SetCompression(false);` Because it's missing the DLL MonoPosixHelper from the build
* `QBitNinjaClient client = new QBitNinjaClient("http://api.qbit.ninja/", NBitcoin.Network.Main);` because HTTPS with `HttpClient` seems to not work correctly.
* Scripting Runtime Version: Select "Experimental (.NET 4.6 Equivalent)"

License
-------
This project is released under the terms of the GPLv3 license. See [LICENSE](LICENSE) for more information or see http://opensource.org/licenses/GPL-3.0.
