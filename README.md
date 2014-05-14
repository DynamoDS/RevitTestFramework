RevitTestFramework
==================

The Revit Test Framework (RTF) allows you to conduct remote unit testing on Revit. RTF takes care of creating a journal file for running revit which can specify a model to start Revit, and a specific test or fixture of tests to Run. You can even specify a model to open before testing and RTF will do that as well. 

RTF's gui allows you to choose tests from a treeview and to visualize the results of the tests as they are run. RTF can also be run as a command line process. In either case, the output file from a test run is an nunit results file compatible with many CI systems.
