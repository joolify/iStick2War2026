/* Favorites Tab[s] for Unity
* version 1.3.2, December 2025
 * Copyright © 2012-2025, by Flipbook Games
 * 
 * Your personalized list of favorite assets and scene objects
 * 
 * Follow @FlipbookGames on http://twitter.com/FlipbookGames
 * Like Flipbook Games on Facebook http://facebook.com/FlipbookGames
 * Join Unity forum discusion http://forum.unity3d.com/threads/149856
 * Contact info@flipbookgames.com for feedback, bug reports, or suggestions.
 * Visit http://flipbookgames.com/ for more info.
 */

namespace FavoritesTabs
{
	using UnityEngine;
	using System.Collections.Generic;

	[ExecuteAlways]
	public class FG_GameObjectGUIDs : MonoBehaviour
	{
		public static bool _dirty = true;
	
		public static HashSet<FG_GameObjectGUIDs> allInstances = new HashSet<FG_GameObjectGUIDs>();
	
		public static void Test(){}

		protected FG_GameObjectGUIDs() { _dirty = true; }
		protected void Awake() { _dirty = allInstances.Add(this) || _dirty; }
		protected void OnEnable() { _dirty = allInstances.Add(this) || _dirty; }
		protected void OnDisable() { _dirty = true; }
		protected void OnDestroy() { _dirty = allInstances.Remove(this) || _dirty; }
	
		[SerializeField, HideInInspector]
		public List<string> guids = new List<string>();
		[SerializeField, HideInInspector]
		public List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
	}
}
