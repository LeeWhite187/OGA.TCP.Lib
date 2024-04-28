# OGA.TCP.Lib
Message-Based TCP Library for NET Framework and Core


Published as two separate nuget packages for client and server.<br>
The client library nuget package covers the following:<br>
* NET Framework 4.5.2
* NET Framework 4.8
* NET Standard 2.1
* NET 5
* NET 6
* NET 7

The server library nuget package covers the following:<br>
* NET 5
* NET 6
* NET 7


NOTE:
This repository contains two VS solutions.
* OGA.TCP.Lib - This is the main library solution, and where all development should be done.
* OGA.TCP.Lib_forNET452Test - This solution was created as a workaround, so the NET4.5.2 library target can be tested.
  This has to be done, since NET4.5.2 is only compatible with earlier MSTest framework versions.
  And, the MSTest framework myopically ONLY uses the latest MSTest framework dll versions it finds across the entire solution, instead of the specific version for a test project.
  So, we had to isolate testing of the NET4.5.2 library to its own solution, so the tests will execute.
See this article for more details: https://oga.atlassian.net/wiki/spaces/~311198967/pages/191430698/.NET+Framework+Unit+Testing+Issues
