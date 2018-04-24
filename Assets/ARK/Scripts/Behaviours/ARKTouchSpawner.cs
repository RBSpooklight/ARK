using UnityEngine;

namespace Kouji.ARK
{
	public class ARKTouchSpawner : ARKObject
	{
		[SerializeField] private GameObject m_objectToPlace;
		
		private Transform root;

		private void OnEnable()
		{
			root = transform;
		}

		void Update()
		{
			if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
			{
				var camera = GetCamera();
				var ray = camera.ScreenPointToRay(Input.GetTouch(0).position);
				var layerMask = 1 << LayerMask.NameToLayer("ARObject");

				RaycastHit hit;
				if (Physics.Raycast(ray, out hit, float.MaxValue, layerMask))
				{
					GameObject go = Instantiate(m_objectToPlace, root);
					go.transform.position = hit.point;

					var pose = new Pose();
					ARKInterface.GetInterface().TryGetPose(ref pose);
					
					var relativePos = pose.position - hit.point;
					go.transform.rotation = Quaternion.LookRotation(relativePos);
				}
			}
		}
	}
}
