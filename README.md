##RevitTestFramework

The Revit Test Framework (RTF) allows you to conduct remote unit testing on Revit. RTF takes care of creating a journal file for running revit which can specify a model to start Revit, and a specific test or fixture of tests to Run. You can even specify a model to open before testing and RTF will do that as well. 

RTF has two executables. 

RevitTestFrameworkGUI.exe allows you to choose tests from a treeview and to visualize the results of the tests as they are run.

RevitTestFrameworkConsole.exe is a console application which allows running RTF without a user interface. If you'd like to learn more about the command line options for RTF, you can simply type "RevitTestFrameworkConsole -h" and you'll get something like this:

    Usage: RevitTestFrameworkConsole [OPTIONS]
    Run a test or a fixture of tests from an assembly.

    Options:
          --dir[=VALUE]             The path to the working directory.
      -a, --assembly[=VALUE]        The path to the test assembly.
      -r, --results[=VALUE]         The path to the results file.
      -f, --fixture[=VALUE]         The full name (with namespace) of the test fixture.
      -t, --testName[=VALUE]        The name of a test to run
      -c, --concatenate[=VALUE]     Concatenate results with existing results file.
          --revit[=VALUE]           The Revit executable.
      -d, --debug                   Run in debug mode.
      -h, --help                    Show this message and exit.

##Results  

The output file from a test run is an nunit-formatted results file compatible with many CI systems.

##Command Line Parameters

**--dir**

The working directory is the directory in which RTF will generate the journal and the addin to Run Revit. Revit's run-by-journal capability requires that all addins which need to be loaded are in the same directory as the journal file. So, if you're testing other addins on top of Revit using RTF, you'll need to put those addins in whatever directory you specify as the working directory.

**--assembly**  
This is the full path to the assembly that contains your tests.

**--results**  
This is the full path to an .xml file that will contain the results.

**--fixture** (Optional)  
The name of a test fixture to run. If no fixture and no test names are specified, RTF will run all tests in the assembly.

**--testName** (Optional)  
The name of a test to run. If no test and no fixture names are specified, RTF will run all tests in the assembly.

**--concatenate** (Optional)  
Should the results from this run of RTF be added to an existing results file if one exists at the path specified. The default behavior is to replace the existing results file.

**--revit** (Optional)  
Specify a Revit executable to use for testing. **You should ensure that you specify the correct version of Revit and are running the correct version of RTF (See "Revit Versions" below.)**

**--debug** (Optional)  
Should RTF attempt to attach to a debugger?

**--help**  
Help!

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
