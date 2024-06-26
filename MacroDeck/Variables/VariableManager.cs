﻿using System.Text.RegularExpressions;
using System.Threading;
using SQLite;
using SuchByte.MacroDeck.CottleIntegration;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Plugins;
using SuchByte.MacroDeck.Startup;

namespace SuchByte.MacroDeck.Variables;

public static class VariableManager
{
    private static SQLiteConnection _database;
    
    internal static event EventHandler OnVariableChanged;
    internal static event EventHandler OnVariableRemoved;


    /// <summary>
    /// Use GetVariables(MacroDeckPlugin macroDeckPlugin)
    /// </summary>
    internal static TableQuery<Variable> ListVariables => 
        _database.Table<Variable>().OrderBy(v => v.Name);

    public static Variable[] Variables =>
        ListVariables.ToArray();

    public static List<Variable> GetVariables(MacroDeckPlugin macroDeckPlugin)
    {
        return _database.Table<Variable>()
            .Where(x => x.Creator.ToLower() == macroDeckPlugin.Name.ToLower())
            .OrderBy(x => x.Name)
            .ToList();
    }

    public static Variable GetVariable(MacroDeckPlugin macroDeckPlugin, string variableName)
    {
        return ListVariables.FirstOrDefault(x =>
            x.Creator == macroDeckPlugin.Name &&
            x.Name.ToLower() == variableName.ToLower());
    }
    
    internal static void InsertVariable(Variable variable)
    {
        if (ListVariables.Any(x => x.Name.ToLower() == variable.Name.ToLower()))
        {
            return;
        }
        _database.Insert(variable);
    }

    internal static Variable? SetValue(string name, object value, VariableType type, string creator = "User")
    {
        name = ConvertNameString(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        
        var variable = _database.Table<Variable>().FirstOrDefault(v => v.Name == name);
        if (variable == null)
        {
            variable = new Variable
            {
                Name = name
            };
            _database.Insert(variable);
        }
        variable.Type = type.ToString();
        variable.Creator = creator;

        if (variable.Value.Equals(value))
        {
            return variable;
        }

        switch (type)
        {
            case VariableType.Bool:
                if (bool.TryParse(value.ToString(), out var boolResult))
                {
                    value = boolResult;
                }
                else
                {
                    value = false;
                }
                variable.Value = value.ToString();
                break;
            case VariableType.Float:
                if (float.TryParse(value.ToString().Replace(".", Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator), out var floatResult))
                {
                    value = floatResult;
                }
                else
                {
                    value = 0.0f;
                }
                variable.Value = ((float)value).ToString();
                break;
            case VariableType.Integer:
                if (int.TryParse(value.ToString(), out var intResult))
                {
                    value = intResult;
                }
                else
                {
                    value = 0;
                }
                variable.Value = value.ToString();
                break;
            case VariableType.String:
            default:
                variable.Value = value.ToString();
                break;
        }

        try
        {
            _database.Update(variable);
        }
        catch (Exception ex)
        {
            MacroDeckLogger.Error($"Error while updating variable {variable.Name}:\n{ex.Message}");
        }

        OnVariableChanged?.Invoke(variable, EventArgs.Empty);

        return variable;
    }

    internal static Variable SetValue(string name, object value, VariableType type, string[] suggestions, string creator = "User")
    {
        var variable = SetValue(name, value, type, creator);
        variable.Suggestions = suggestions;

        return variable;
    }
        
    /// <summary>
    /// Set the value of an variable. If the variable does not exists, Macro Deck automatically creates it.
    /// </summary>
    /// <param name="name">Name of the variable</param>
    /// <param name="value">Value of the variable</param>
    /// <param name="type">Type of the variable</param>
    /// <param name="plugin">The instance of your plugin</param>
    /// <param name="suggestions">Suggestions for the value.</param>
    public static void SetValue(string name, object value, VariableType type, MacroDeckPlugin plugin, string[] suggestions = null)
    {
        SetValue(name, value, type, suggestions, plugin.Name);
    }

    [Obsolete("Remove save parameter")]
    public static void SetValue(string name, object value, VariableType type, MacroDeckPlugin plugin, bool save = true)
    {
        SetValue(name, value, type, plugin.Name);
    }


    [Obsolete("Remove save parameter")]
    public static void SetValue(string name, object value, VariableType type, MacroDeckPlugin plugin, string[] suggestions, bool save = true)
    {
        SetValue(name, value, type, suggestions, plugin.Name);
    }

    /// <summary>
    /// Deletes a variable
    /// </summary>
    /// <param name="name">Name of the variable</param>
    public static void DeleteVariable(string name)
    {
        _database.Delete(new Variable() { Name = name });
        OnVariableRemoved?.Invoke(name, EventArgs.Empty);
        MacroDeckLogger.Trace("Deleted variable " + name);
    }

    [Obsolete("Use TemplateManager.RenderTemplate")]
    public static string RenderTemplate(string template)
    {
        return TemplateManager.RenderTemplate(template);
    }

    internal static void Initialize()
    {
        MacroDeckLogger.Info(typeof(VariableManager), "Initialize variables database...");
        _database = new SQLiteConnection(ApplicationPaths.VariablesFilePath);
            
        _database.CreateTable<Variable>();
        _database.Table<Variable>().Where(x => x.Name == "").Delete();
        MacroDeckLogger.Info(typeof(VariableManager), ListVariables.Count() + " variables found");
    }

    internal static void Close()
    {
        try
        {
            _database.Close();
        } catch { }
    }

    public static string ConvertNameString(string str)
    {
        str = str.ToLower();

        var regexSpecialChars = new Regex("[ |\\.\\-/]");
        str = regexSpecialChars.Replace(str, "_");

        var regexUmlauts = new Regex("[äöüß]");
        var evaluator = new MatchEvaluator(UmlautsReplacer);
        return regexUmlauts.Replace(str, evaluator);
    }
    
    private static string UmlautsReplacer(Match m)
    {
        return m.Value switch
        {
            "ä" => "ae",
            "ö" => "oe",
            "ü" => "ue",
            "ß" => "ss",
            _ => m.Value
        };
    }
}