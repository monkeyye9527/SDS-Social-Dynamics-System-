namespace ONIModPack.Content.Art
{
    public class SlotsYamlConfig
    {
        public string BuildingName { get; set; }
        public System.Collections.Generic.List<SlotYamlEntry> Slots { get; set; }
            = new System.Collections.Generic.List<SlotYamlEntry>();
        public System.Collections.Generic.List<SlotTintEntry> Tints { get; set; }
            = new System.Collections.Generic.List<SlotTintEntry>();
    }

    public class SlotYamlEntry
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public float Capacity { get; set; }
    }

    public class SlotTintEntry
    {
        public string Name { get; set; }
        public string Color { get; set; }
    }
}