using Common;
using FezEngine.Structure;
using FezEngine.Tools;
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

		public string HelpText => "component <path> [value] - get or set a member of a game component";

		public List<string> Autocomplete(string[] args)
		{
			if (args.Length != 1) return null;

			var path = args[0].Split('.');
			if(path.Length == 1)
			{
				return ComponentList.Where(s => s.ToLower().StartsWith(args[0])).ToList();
			}

			object pov = ServiceHelper.Game.Components.FirstOrDefault(c => c.GetType().Name.ToLower().Equals(path[0]));
			string currentPath = pov.GetType().Name;
			for (int i = 1; i < path.Length - 1; i++)
			{
				if(pov == null)
				{
					return null;
				}

				var field = pov.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(f => f.Name.ToLower().Equals(path[i]));
				if (field == null)
				{
					var prop = pov.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.Name.ToLower().Equals(path[i]));
					if (prop == null)
					{
						return null;
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

				currentPath = currentPath + "." + path[i];
			}

			return (from member 
					in GetBothFieldsAndPropsOf(pov) 
					where member.Name.ToLower().StartsWith(path[path.Length - 1]) 
					select (currentPath + '.' + member.Name)).ToList();
		}

		public IEnumerable<FieldInfo> GetFieldsOf(Object component)
		{
			var fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var fields_filtered = from field in fields where field.GetCustomAttribute<CompilerGeneratedAttribute>() == null select field;
			return from field in fields_filtered orderby field.Name select field;
		}

		public IEnumerable<PropertyInfo> GetPropsOf(Object component)
		{
			var props = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return from prop in props orderby prop.Name select prop;
		}

		public IEnumerable<MemberInfo> GetBothFieldsAndPropsOf(Object component)
		{
			return from member 
				   in GetFieldsOf(component).Concat<MemberInfo>(GetPropsOf(component)) 
				   orderby member.Name 
				   select member;
		}

		public bool SetMemberValue<T>(MemberInfo member, object obj, T value, object[] index = null)
		{
			if(member as FieldInfo != null)
			{
				(member as FieldInfo).SetValue(obj, value);
				return true;
			}
			else if(member is PropertyInfo)
			{
				(member as PropertyInfo).SetValue(obj, value, index);
				return true;
			}
			return false;
		}

		public object GetMemberValue(MemberInfo member, object obj, object[] index = null)
		{
			if (member as FieldInfo != null)
			{
				return (member as FieldInfo).GetValue(obj);
			}
			else if (member is PropertyInfo)
			{
				return (member as PropertyInfo).GetValue(obj, index);
			}
			return null;
		}

		public float[] StringAsFloatList(string str, int float_count) 
		{
			string[] string_vals = str.Split(',');
			List<float> vals = new List<float>();
			for(int i = 0; i < string_vals.Length; i++)
			{
				float nextval;
				if (float.TryParse(string_vals[i], out nextval))
				{
					vals.Add(nextval);
				} 
				else
				{
					FezugConsole.Print("Element could not be parsed to float: " + string_vals[i], FezugConsole.OutputType.Error);
					return null;
				}
			}
			if (vals.Count == float_count)
			{
				return vals.ToArray();
			}
			else
			{
				FezugConsole.Print("Incorrect number of elements: expected " + float_count + ", got " + vals.Count, FezugConsole.OutputType.Error);
				return null;
			}
		}

		public bool Execute(string[] args)
		{
			if (args.Length > 2)
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

			// split the first argument (a member path) into argument pieces
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
				FezugConsole.Print("Fields on " + component_type.Name + ":", Color.LightSlateGray);
				FezugConsole.Print(String.Join(", ", from field in GetFieldsOf(component) select field.Name));
				FezugConsole.Print("Props on " + component_type.Name + ":", Color.LightSlateGray);
				FezugConsole.Print(String.Join(", ", from field in GetPropsOf(component) select field.Name));
				return true;
			}

			var currentPath = component_type.Name;
			object last_pov = component;
			object pov = component;
			MemberInfo pov_member = null;
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
						last_pov = pov;
						pov = prop.GetValue(pov);
						currentPath = currentPath + "." + prop.Name;
						pov_member = prop;
					}
				}
				else
				{
					last_pov = pov;
					pov = field.GetValue(pov);
					currentPath = currentPath + "." + field.Name;
					pov_member = field;
				}
			}

			string newValArg = args.ElementAtOrDefault(1);
			if (newValArg != null)
			{
				FieldInfo pov_field = pov_member as FieldInfo;
				PropertyInfo pov_prop = pov_member as PropertyInfo;
				Type pov_member_type = null;
				if (pov_field != null)
				{
					pov_member_type = pov_field.FieldType;
				}
				else if (pov_prop != null)
				{
					pov_member_type = pov_prop.PropertyType;
				} 
				else
				{
					FezugConsole.Print("Member at path " + currentPath + " is somehow neither a field nor a property!", FezugConsole.OutputType.Error);
					return false;
				}

				// handle types that don't cleanly convert from string (mostly those that require explicit parsing)
				// this is messy and patchwork but it should hit a lot of common stuff
				// I think there has to be a better way of generalizing it but I'm in "implementation" not "optimization" rn
				if (pov_member_type == typeof(int))
				{
					int newValue_i;
					if (int.TryParse(newValArg, out newValue_i))
					{
						SetMemberValue(pov_member, last_pov, newValue_i);
					}
					else
					{
						FezugConsole.Print("Could not parse \"" + newValArg + "\" as an int value for " + currentPath + "!", FezugConsole.OutputType.Error);
						return false;
					}
				}
				else if (pov_member_type == typeof(float))
				{
					float newValue_f;
					if (float.TryParse(newValArg, out newValue_f))
					{
						SetMemberValue(pov_member, last_pov, newValue_f);
					}
					else
					{
						FezugConsole.Print("Could not parse \"" + newValArg + "\" as a float value for " + currentPath + "!", FezugConsole.OutputType.Error);
						return false;
					}
				} 
				else if (pov_member_type == typeof(Vector2))
				{
					float[] vec2_elems = StringAsFloatList(newValArg, 2);
					if(vec2_elems != null)
					{
						SetMemberValue(pov_member, last_pov, new Vector2(vec2_elems[0], vec2_elems[1]));
					}
				}
				else if (pov_member_type == typeof(Vector3))
				{
					float[] vec3_elems = StringAsFloatList(newValArg, 3);
					if (vec3_elems != null)
					{
						SetMemberValue(pov_member, last_pov, new Vector3(vec3_elems[0], vec3_elems[1], vec3_elems[2]));
					}
				}
				else if (pov_member_type == typeof(bool))
				{
					var value_character = newValArg.Substring(0, 1).ToLower();
					if(value_character.Equals("1") || value_character.Equals("t") || value_character.Equals("y"))
					{
						SetMemberValue(pov_member, last_pov, true);
					}
					else if(value_character.Equals("0") || value_character.Equals("f") || value_character.Equals("n"))
					{
						SetMemberValue(pov_member, last_pov, false);
					}
					else
					{
						FezugConsole.Print("\"" + newValArg + "\" doesn't seem like a bool value for " + currentPath + "!", FezugConsole.OutputType.Error);
						return false;
					}
				}
				else if (pov_member_type == typeof(string))
				{
					SetMemberValue(pov_member, last_pov, newValArg);
				} 
				else
				{
					FezugConsole.Print("Member \"" + currentPath + "\" cannot be set because it is not of type: int, float, Vector2, Vector3, bool, string", FezugConsole.OutputType.Error);
					return false;
				}
			}

			FezugConsole.Print(currentPath + " = " + GetMemberValue(pov_member, last_pov));

			return true;
		}
	}
}
