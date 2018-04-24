using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kouji.ARK 
{
	public class ARKPlaneViewer : ARObject
	{
		[SerializeField] private GameObject m_planePrefab;
		
		private LayerMask m_planeLayer;
		private readonly Dictionary<string, GameObject> m_planes = new Dictionary<string, GameObject>();

		private void OnEnable()
		{
			m_planeLayer = LayerMask.NameToLayer("ARObject");

			ARKInterface.onPlaneAdded += OnPlaneAdded;
			ARKInterface.onPlaneRemoved += OnPlaneRemoved;
			ARKInterface.onPlaneUpdated += OnPlaneUpdated;
		}
		
		private void OnDisable()
		{
			ARKInterface.onPlaneAdded -= OnPlaneAdded;
			ARKInterface.onPlaneRemoved -= OnPlaneRemoved;
			ARKInterface.onPlaneUpdated -= OnPlaneUpdated;
		}
		
		protected virtual void CreateOrUpdatePlaneGameObject(TrackedPlane _plane)
		{
			GameObject go;
			if (!m_planes.TryGetValue(_plane.id, out go))
			{
				go = Instantiate(m_planePrefab, GetRoot());
	
				//Viewer contains colliders, we need to assign the correct layer for Raycasts
				foreach (var col in go.GetComponentsInChildren<Collider>())
					col.gameObject.layer = m_planeLayer;
				
				m_planes.Add(_plane.id, go);
			}

			go.transform.localPosition = _plane.center;
			go.transform.localRotation = _plane.rotation;
			go.transform.localScale = new Vector3(_plane.extents.x, 1f, _plane.extents.y);
		}
		
		protected virtual void OnPlaneAdded(TrackedPlane _plane)
		{
			if (m_planePrefab)
				CreateOrUpdatePlaneGameObject(_plane);
		}

		protected virtual void OnPlaneUpdated(TrackedPlane _plane)
		{
			if (m_planePrefab)
				CreateOrUpdatePlaneGameObject(_plane);
		}

		protected virtual void OnPlaneRemoved(TrackedPlane _plane)
		{
			GameObject go;
			if (m_planes.TryGetValue(_plane.id, out go))
			{
				Destroy(go);
				m_planes.Remove(_plane.id);
			}
		}
	}
}
