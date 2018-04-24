using UnityEngine;

namespace Kouji.ARK 
{
	public sealed class ARKAnchor : ARKObject
	{
		[HideInInspector] public string id;

		private ARKInterface m_interface;
		private bool isStarted;

		private void Awake()
		{
			m_interface = ARKInterface.GetInterface();
			if (m_interface == null)
				Destroy(this);
		}
		
		private void Start()
		{
			UpdateAnchor();
			isStarted = true;
		}

		private void OnEnable()
		{
			if (isStarted)
				UpdateAnchor();
		}

		private void OnDisable()
		{
			m_interface.DestroyAnchor(this);
		}

		private void OnDestroy()
		{
			m_interface.DestroyAnchor(this);
		}

		public void UpdateAnchor() 
		{
			m_interface.DestroyAnchor(this);
			m_interface.ApplyAnchor(this);
		}
	}
}
