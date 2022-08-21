using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MapUISystem : SystemBase
{
    VisualElement coordinates;
    private int maxSectorSegments;
    private int maxSubSectorSegments;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<LocalPlayer>();
        maxSectorSegments = sizeof(int);
        for (int i = 0; i < sizeof(long) && (Constants.MAX_SUBSECTOR & (0x1FF << (i*9))) > 0; i++)
        {
            maxSubSectorSegments = i + 1;
        }
    }

    protected override void OnStartRunning()
    {
        if (coordinates == null)
        {
            coordinates = GameObject.FindObjectOfType<UIDocument>().rootVisualElement.Q("Coordinates");
            coordinates.Query(null, "coorsys-segment-container").ForEach(x => x.Clear());
            coordinates.Query("Sector", "coorsys-segment-container").ForEach(x =>
            {
                for (int i = 0; i < maxSectorSegments; i++)
                {
                    var ve = new VisualElement();
                    ve.AddToClassList("coorsys-segment");
                    x.Add(ve);
                }
            });

            coordinates.Query("SubSector", "coorsys-segment-container").ForEach(x =>
            {
                for (int i = 0; i < maxSubSectorSegments; i++)
                {
                    var ve = new VisualElement();
                    ve.AddToClassList("coorsys-segment");
                    x.Add(ve);
                }
            });
        }
    }

    protected override void OnUpdate()
    {
        var ltg = GetComponent<LocalToGalaxy>(GetSingletonEntity<LocalPlayer>());
        var secX = coordinates.Q("X").Q("Sector");
        for (int i = 0; i < secX.childCount; i++)
        {
            var seg = (short)((ltg.Sector.x >> (9*i)) & 0x1FF);
            secX[i].ClearClassList();
            secX[i].AddToClassList("coorsys-segment");
            secX[i].AddToClassList($"segment-{seg}");
        }

        var secY = coordinates.Q("Y").Q("Sector");
        for (int i = 0; i < secY.childCount; i++)
        {
            var seg = (short)((ltg.Sector.y >> (9*i)) & 0x1FF);
            secY[i].ClearClassList();
            secY[i].AddToClassList("coorsys-segment");
            secY[i].AddToClassList($"segment-{seg}");
        }

        var secZ = coordinates.Q("Z").Q("Sector");
        for (int i = 0; i < secZ.childCount; i++)
        {
            var seg = (short)((ltg.Sector.z >> (9*i)) & 0x1FF);
            secZ[i].ClearClassList();
            secZ[i].AddToClassList("coorsys-segment");
            secZ[i].AddToClassList($"segment-{seg}");
        }

        var secXS = coordinates.Q("X").Q("SubSector");
        for (int i = 0; i < secXS.childCount; i++)
        {
            var seg = (short)((ltg.SubSector.x >> (9*i)) & 0x1FF);
            secXS[i].ClearClassList();
            secXS[i].AddToClassList("coorsys-segment");
            secXS[i].AddToClassList($"segment-{seg}");
        }

        var secYS = coordinates.Q("Y").Q("SubSector");
        for (int i = 0; i < secYS.childCount; i++)
        {
            var seg = (short)((ltg.SubSector.y >> (9*i)) & 0x1FF);
            secYS[i].ClearClassList();
            secYS[i].AddToClassList("coorsys-segment");
            secYS[i].AddToClassList($"segment-{seg}");
        }

        var secZS = coordinates.Q("Z").Q("SubSector");
        for (int i = 0; i < secZS.childCount; i++)
        {
            var seg = (short)((ltg.SubSector.z >> (9*i)) & 0x1FF);
            secZS[i].ClearClassList();
            secZS[i].AddToClassList("coorsys-segment");
            secZS[i].AddToClassList($"segment-{seg}");
        }

        var speed = GetComponent<CanTravelSectors>(GetSingletonEntity<LocalPlayer>());
        coordinates.Q<Label>().text = $"{GalaxyTools.GetSectorType(ltg.Sector)} > {GalaxyTools.GetSubSectorData(ltg).Type}\n" +
                                      $"{GalaxyTools.ToGalacticDistanceWithUnit(speed.CurrentSpeed)}/s";
    }
}
