// description: Pretty prints command output if a shell command ends with a #
// author: @therealshodan

// After the command has been replaced this function will be executed
void ExecPrettyPrint(string command)
{
  // as we need to pass a variable into a command we need to use the $ syntax
  var commandOutput=`$command$`;

  // PrettyPrint has already been loaded
  PrettyPrint(commandOutput, true);
}

// This function just performs text replacement
// We don't actually want to execute new commands here
string PrettyPrintHandler(string command)
{
  if (command.EndsWith("#"))
  {
    command = "ExecPrettyPrint(\"" + command.TrimEnd('#') + "\");";
  }

  return command;
}

Shell.CommandHandlers.Add(PrettyPrintHandler);
