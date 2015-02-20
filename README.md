##RevitTestFramework

The Revit Test Framework (RTF) allows you to conduct remote unit testing on Revit. RTF takes care of creating a journal file for running revit which can specify a model to start Revit, and a specific test or fixture of tests to Run. You can even specify a model to open before testing and RTF will do that as well. 

RTF has two executables. 

RevitTestFrameworkGUI.exe allows you to choose tests from a treeview and to visualize the results of the tests as they are run.

RevitTestFrameworkConsole.exe is a console application which allows running RTF without a user interface. If you'd like to learn more about the command line options for RTF, you can simply type "RevitTestFrameworkConsole -h" and you'll get something like this:
```
  Options:   
         --dir=[VALUE]          The full path to the working directory. The working directory is the directory in which RTF will generate the journal and the addin to Run Revit. Revit's run-by-journal capability requires that all addins which need to be loaded are in the same directory as the journal file. So, if you're testing other addins on top of Revit using RTF, you'll need to put those addins in whatever directory you specify as the working directory.  
    -a,  --assembly=[VALUE]     The full path to the assembly containing your tests.  
    -r,  --results=[VALUE]      This is the full path to an .xml file that will contain the results. 
    -f,  --fixture=[VALUE]      The full name (with namespace) of a test fixture to run. If no fixture, no category and no test names are specified, RTF will run all tests in the assembly.(OPTIONAL)  
    -t,  --testName[=VALUE]     The name of a test to run. If no fixture, no category and no test names are specified, RTF will run all tests in the assembly. (OPTIONAL)    
         --category[=VALUE]     The name of a test category to run. If no fixture, no category and no test names are specified, RTF will run all tests in the assembly. (OPTIONAL)   
         --exclude[=VALUE]      The name of a test category to exclude. This has a higher priortiy than other settings. If a specified category is set here, any test cases that belongs to that category will not be run. (OPTIONAL)  
    -c,  --concatenate          Concatenate the results from this run of RTF with an existing results file if one exists at the path specified. The default behavior is to replace the existing results file. (OPTIONAL)  
         --revit[=VALUE]        The Revit executable to be used for testing. If no executable is specified, RTF will use the first version of Revit that is found on the machine using the RevitAddinUtility. (OPTIONAL)  
         --copyAddins           Specify whether to copy the addins from the Revit folder to the current working directory. Copying the addins from the Revit folder will cause the test process to simulate the typical setup on your machine. (OPTIONAL)  
         --dry                  Conduct a dry run. (OPTIONAL)  
    -x,  --clean                Cleanup journal files after test completion. (OPTIONAL)   
         --continuous           Run all selected tests in one Revit session. (OPTIONAL)  
    -d,  --debug                Should RTF attempt to attach to a debugger?. (OPTIONAL)  
    -h,  --help                 Show this message and exit. (OPTIONAL)  
```

##Results  

The output file from a test run is an nunit-formatted results file compatible with many CI systems.

##Revit Versions

There are two branches in this repository which track two versions of Revit. The master branch tracks Revit 2014, while the Revit2015 branch tracks Revit 2015. This will, most likely, change in the future. When testing, you should run the version of RTF corresponding to the version of Revit you are running. This will ensure that tests you have created, based on one Revit API, will correspond to the version of the API running on Revit.

##License

Copyright 2014 Autodesk

Licensed under The MIT License; you may not use this file except in compliance with the License. You may obtain a copy of the License at

http://opensource.org/licenses/MIT

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
