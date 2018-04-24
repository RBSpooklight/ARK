using UnityEngine;

namespace Kouji.ARK
{
	public class ARPlaceable : ARObject
	{
		/// <summary>
		/// Usually the Augmentation Root
		/// </summary>
		[SerializeField] private Transform m_objectToPlace;

		void Update()
		{
			if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
			{
				var camera = GetCamera();
				var ray = camera.ScreenPointToRay(Input.GetTouch(0).position);

				int layerMask = 1 << LayerMask.NameToLayer("ARObject");

				RaycastHit hit;
				if (Physics.Raycast(ray, out hit, float.MaxValue, layerMask))
					m_objectToPlace.transform.position = hit.point;
			}
		}
	}
}
