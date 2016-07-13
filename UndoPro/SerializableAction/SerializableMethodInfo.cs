﻿namespace UndoPro.SerializableActionHelper
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using UnityEngine;
	using System.Runtime.CompilerServices;

	/// <summary>
	/// Wrapper for MethodInfo that handles serialization.
	/// Stores declaringType, methodName, parameters and flags only and supports generic types (one level for class, two levels for method).
	/// </summary>
	[Serializable]
	public class SerializableMethodInfo : ISerializationCallbackReceiver
	{
		public MethodInfo methodInfo;

		[SerializeField]
		private SerializableType declaringType;
		[SerializeField]
		private string methodName;
		[SerializeField]
		private List<SerializableType> parameters = null;
		[SerializeField]
		private List<SerializableType> genericTypes = null;
		[SerializeField]
		private int flags = 0;

		// Accessors
		public string SignatureName { get { return (((BindingFlags)flags&BindingFlags.Public) != 0? "public" : "private") + (((BindingFlags)flags&BindingFlags.Static) != 0? " static" : "") + " " + methodName; } }
		public bool IsAnonymous { get { return Attribute.GetCustomAttribute (methodInfo, typeof(CompilerGeneratedAttribute), false) != null || declaringType.isCompilerGenerated; } }

		public SerializableMethodInfo (MethodInfo MethodInfo)
		{
			methodInfo = MethodInfo;
		}

		#region Serialization

		public void OnBeforeSerialize()
		{
			if (methodInfo == null)
				return;

			declaringType = new SerializableType (methodInfo.DeclaringType);
			methodName = methodInfo.Name;

			// Flags
			if (methodInfo.IsPrivate)
				flags |= (int)BindingFlags.NonPublic;
			else
				flags |= (int)BindingFlags.Public;
			if (methodInfo.IsStatic)
				flags |= (int)BindingFlags.Static;
			else
				flags |= (int)BindingFlags.Instance;

			// Parameter
			ParameterInfo[] param = methodInfo.GetParameters ();
			if (param != null && param.Length > 0)
				parameters = param.Select ((ParameterInfo p) => new SerializableType (p.ParameterType)).ToList ();
			else
				parameters = null;

			// Generic types
			if (methodInfo.IsGenericMethod)
			{
				methodName = methodInfo.GetGenericMethodDefinition ().Name;
				genericTypes = methodInfo.GetGenericArguments ().Select ((Type genArgT) => new SerializableType (genArgT)).ToList ();
			}
			else
				genericTypes = null;
		}

		public void OnAfterDeserialize()
		{
			if (declaringType == null || declaringType.type == null || string.IsNullOrEmpty (methodName))
				return;

			// Parameters
			Type[] param;
			if (parameters != null && parameters.Count > 0) // With parameters
				param = parameters.Select ((SerializableType t) => t.type).ToArray ();
			else 
				param = new Type[0];

			methodInfo = declaringType.type.GetMethod (methodName, (BindingFlags)flags, null, param, null);
			if (methodInfo == null)
			{ // Retry with private flags, because in some compiler generated methods flags will be uncertain (?) which then return public but are private
				methodInfo = declaringType.type.GetMethod (methodName, (BindingFlags)flags | BindingFlags.NonPublic, null, param, null);
				if (methodInfo == null)
					throw new Exception ("Could not deserialize '" + SignatureName + "' in declaring type '" + declaringType.type.FullName + "'!");
			}

			if (methodInfo.IsGenericMethodDefinition && genericTypes != null && genericTypes.Count > 0)
			{ // Generic Method
				Type[] genArgs = genericTypes.Select ((SerializableType t) => t.type).ToArray ();

				MethodInfo genMethod = methodInfo.MakeGenericMethod (genArgs);	
				if (genMethod != null)
					methodInfo = genMethod;
				else 
					Debug.LogError ("Could not make generic-method definition '" + methodName + "' generic!");
			}
		}

		#endregion
	}
}