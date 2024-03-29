﻿using FezEngine.Tools;
using FEZUG.Features.Console;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace FEZUG.Features
{
	internal class ComponentInfoCommand : IFezugCommand
	{
		public ComponentInfoCommand() { }

		public static List<string> ComponentList
		{
			get
			{
				return ServiceHelper.Game.Components.Select(c => c.GetType().Name).ToList();
			}
		}

		public string Name => "component";

		public string HelpText => "Get a property at a path on a component.";

		public List<string> Autocomplete(string[] args)
		{
			// if (args.Length == 1)
			// {
			// 	return ComponentList.Where(s => s.ToLower().StartsWith(args[0])).ToList();
			// }
			// if (args.Length == 2)
			// {
			// 	var component = ServiceHelper.Game.Components.FirstOrDefault(c => c.GetType().Name.ToLower().Equals(args[0]));
			// 	if (component != null)
			// 	{
			// 		return GetFieldsOf(component).Where(f => f.ToLower().StartsWith(args[1])).ToList();
			// 	}
			// }
			return null;
		}

		public IEnumerable<MemberInfo> GetFieldsOf(IGameComponent component)
		{
			var fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var fields_filtered = from field in fields where field.GetCustomAttribute<CompilerGeneratedAttribute>() == null select field;
			var props = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return from field in fields_filtered.Concat<MemberInfo>(props) orderby field.Name select field;
		}

		public bool Execute(string[] args)
		{
			if (args.Length > 1)
			{
				FezugConsole.Print($"Incorrect number of parameters: '{args.Length}'", FezugConsole.OutputType.Warning);
				return false;
			}

			if (args.Length < 1)
			{
				FezugConsole.Print("List of available components:");
				FezugConsole.Print(String.Join(", ", ComponentList));
				return true;
			}

			var path = args[0].Split('.');

			var component = ServiceHelper.Game.Components.FirstOrDefault(c => c.GetType().Name.ToLower().Equals(path[0]));
			if (component == null)
			{
				FezugConsole.Print("Component does not exist: \"" + args[0] + "\"", FezugConsole.OutputType.Error);
				return false;
			}
			var component_type = component.GetType();

			if (path.Length < 2)
			{
				FezugConsole.Print("List of accessible members on " + args[0] + ":");
				FezugConsole.Print(String.Join(", ", from field in GetFieldsOf(component) select field.Name));
			}

			var currentPath = component_type.Name;
			Object pov = component;
			for (int i = 1; i < path.Length; i++)
			{
				var tempcurrentPath = currentPath + "." + path[i];
				var field = pov.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(f => f.Name.ToLower().Equals(path[i]));
				if (field == null)
				{
					var prop = pov.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.Name.ToLower().Equals(path[i]));
					if (prop == null)
					{
						FezugConsole.Print("Member at path " + tempcurrentPath + " does not exist!", FezugConsole.OutputType.Error);
						return false;
					}
					else
					{
						pov = prop.GetValue(pov);
					}

				}
				else
				{
					pov = field.GetValue(pov);
				}
				currentPath = currentPath + "." + field.Name;
			}

			FezugConsole.Print(currentPath + " = " + pov);

			return true;
		}
	}
}