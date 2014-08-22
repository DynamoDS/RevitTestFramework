##RevitTestFramework

The Revit Test Framework (RTF) allows you to conduct remote unit testing on Revit. RTF takes care of creating a journal file for running revit which can specify a model to start Revit, and a specific test or fixture of tests to Run. You can even specify a model to open before testing and RTF will do that as well. 

RTF's gui allows you to choose tests from a treeview and to visualize the results of the tests as they are run. RTF can also be run as a command line process. In either case, the output file from a test run is an nunit results file compatible with many CI systems.

If you'd like to learn more about the command line options for RTF, you can simply type "RevitTestFrameworkRunner -h" and you'll get something like this:

    Usage: DynamoTestFrameworkRunner [OPTIONS]
    Run a test or a fixture of tests from an assembly.

    Options:
          --dir[=VALUE]             The path to the working directory.
      -a, --assembly[=VALUE]        The path to the test assembly.
      -r, --results[=VALUE]         The path to the results file.
      -f, --fixture[=VALUE]         The full name (with namespace) of the test fixture.
      -t, --testName[=VALUE]        The name of a test to run
      -c, --concatenate[=VALUE]     Concatenate results with existing results file.
          --gui[=VALUE]                 Show the revit test runner gui.
          --revit[=VALUE]               The Revit executable.
      -d, --debug                   Run in debug mode.
      -h, --help                    Show this message and exit.

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

**--gui** (Optional)  
Would you like to see a GUI to allow you to select tests? The default is to run with a gui.

**--revit** (Optional)  
Specify a Revit executable to use for testing.

**--debug** (Optional)  
Should RTF attempt to attach to a debugger?

**--help**  
Help!

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
