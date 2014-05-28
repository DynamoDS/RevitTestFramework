RevitTestFramework
==================

The Revit Test Framework (RTF) allows you to conduct remote unit testing on Revit. RTF takes care of creating a journal file for running revit which can specify a model to start Revit, and a specific test or fixture of tests to Run. You can even specify a model to open before testing and RTF will do that as well. 

RTF's gui allows you to choose tests from a treeview and to visualize the results of the tests as they are run. RTF can also be run as a command line process. In either case, the output file from a test run is an nunit results file compatible with many CI systems.

[code]
Usage: DynamoTestFrameworkRunner [OPTIONS]
Run a test or a fixture of tests from an assembly.

Options:
      --dir[=VALUE]          The path to the working directory.
  -a, --assembly[=VALUE]     The path to the test assembly.
  -r, --results[=VALUE]      The path to the results file.
  -f, --fixture[=VALUE]      The full name (with namespace) of the test
                               fixture.
  -t, --testName[=VALUE]     The name of a test to run
  -c, --concatenate[=VALUE]  Concatenate results with existing results file.
      --gui[=VALUE]          Show the revit test runner gui.
  -d, --debug                Run in debug mode.
  -h, --help                 Show this message and exit.
[/code]
