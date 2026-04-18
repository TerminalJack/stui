//This project is open source. Anyone can use any part of this code however they wish
//Feel free to use this code in your own projects, or expand on this code
//If you have any improvements to the code itself, please visit
//https://github.com/Dharengo/Spriter2UnityDX and share your suggestions by creating a fork
//-Dengar/Dharengo

using UnityEngine;
using System;
using System.Text;
using System.Xml.Serialization;
using System.Collections.Generic;

// All of these classes are containers for the data that is read from the .scml file
// It is directly deserialized into these classes, although some individual values are
// modified into a format that can be used by Unity
namespace Spriter2UnityDX.Importing
{
    [XmlRoot("spriter_data")]
    public class ScmlObject
    {   // Master class that holds all the other data
        [XmlElement("folder")] public List<Folder> folders = new List<Folder>(); // <folder> tags
        [XmlElement("entity")] public List<Entity> entities = new List<Entity>(); // <entity> tags
        [XmlArray("tag_list"), XmlArrayItem("i")] public List<TagListItem> tags = new List<TagListItem>();
    }

    public class Folder : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
        [XmlElement("file")] public List<File> files = new List<File>(); // <file> tags
    }

    public class File : ScmlElement
    {
        public File()
        {
            pivot_x = 0f;
            pivot_y = 1f;
        }

        [XmlAttribute] public string name { get; set; }
        [XmlAttribute] public float pivot_x { get; set; }
        [XmlAttribute] public float pivot_y { get; set; }
        [XmlAttribute("type")] public ObjectType objectType { get; set; }
    }

    public class Entity : ScmlElement
    {
        private string _name;

        [XmlAttribute]
        public string name
        {
            get { return _name; }
            set { _name = EntityNameSanitizer.Sanitize(value); }
        }

        [XmlElement("obj_info")] public List<ObjectInfo> objectInfos = new List<ObjectInfo>();
        [XmlElement("character_map")] public List<CharacterMap> characterMaps = new List<CharacterMap>(); // <character_map> tags
        [XmlElement("animation")] public List<Animation> animations = new List<Animation>(); // <animation> tags
        [XmlArray("var_defs"), XmlArrayItem("i")] public List<VarDef> variableDefs = new List<VarDef>();
    }

    public class ObjectInfo : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
        [XmlAttribute("type")] public ObjectType objectType;

        [XmlAttribute("w")] public float width;
        [XmlAttribute("h")] public float height;

        [XmlAttribute("pivot_x")] public float pivot_x;
        [XmlAttribute("pivot_y")] public float pivot_y;

        [XmlArray("var_defs"), XmlArrayItem("i")] public List<VarDef> variableDefs = new List<VarDef>();
    }

    public class Eventline : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
        // ? (Not used) [XmlAttribute] public int obj { get; set; }

        [XmlElement("key")] public List<SimpleKey> keys = new List<SimpleKey>();
        [XmlElement("meta")] public Metadata metadata;
    }

    public class Metadata
    {
        [XmlElement("varline")] public List<Varline> varlines = new List<Varline>();
        [XmlElement("tagline")] public Tagline tagline;
    }

    public class VarDef : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
        [XmlAttribute("type")] public VarType type;

        [XmlAttribute("default")] public string defaultValue;

        [XmlIgnore] public List<string> possibleStringValues = new List<string>(); // if type is String then this will be populated.
    }

    public class Varline : ScmlElement
    {
        [XmlAttribute] public string name { get; set; } // ? Used?
        [XmlAttribute("def")] public int varDefId; // Id of entry in corresponding collection of VarDefs.
        [XmlElement("key")] public List<VarlineKey> keys = new List<VarlineKey>();
    }

    public class SimpleKey : ScmlElement
    {
        public SimpleKey() { time = 0; }

        private float _time; // In seconds.

        [XmlAttribute]
        public float time
        { //Dengar.NOTE: In Spriter, Time is measured in milliseconds
            // ! Read in seconds.
            // ! Set in milliseconds!
            get { return _time; }
            set { _time = value * 0.001f; } //Dengar.NOTE: In Unity, it is measured in seconds instead, so we need to translate that
        }

        // Use the following when getting (and especially setting) the time so that you know what units you're working in.
        [XmlIgnore] public float time_s { get { return _time; } set { _time = value; }}
    }

    public class SpriterKey : SimpleKey
    {
        [XmlAttribute] public CurveType curve_type { get; set; } // enum : INSTANT,LINEAR,QUADRATIC,CUBIC //Dengar.NOTE (again, no caps)

        [XmlAttribute] public float c1 { get; set; }
        [XmlAttribute] public float c2 { get; set; }
        [XmlAttribute] public float c3 { get; set; }
        [XmlAttribute] public float c4 { get; set; }
    }

    public class VarlineKey : SpriterKey
    {
        [XmlAttribute("val")] public string value;
    }

    public class TagListItem : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
    }

    public class Tagline
    {
        [XmlElement("key")] public List<TaglineKey> keys = new List<TaglineKey>();
    }

    public class TaglineKey : SimpleKey
    {
        [XmlElement("tag")] public List<TagInfo> tags = new List<TagInfo>();
    }

    public class TagInfo : ScmlElement
    {
        [XmlAttribute("t")] public int tagId;
    }

    public enum VarType
    {
        [XmlEnum("string")]
        String,

        [XmlEnum("int")]
        Int,

        [XmlEnum("float")]
        Float
    }

    public class Soundline : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
        [XmlElement("key")] public List<SoundlineKey> keys = new List<SoundlineKey>();
    }

    public class SoundlineKey : SimpleKey
    {
        [XmlElement("object")] public SpriterSound soundObject;
    }

    public class SpriterSound : ScmlElement
    {
        [XmlAttribute("folder")] public int folder;
        [XmlAttribute("file")] public int file;
        [XmlAttribute("panning")] public float panning;
        [XmlAttribute("volume")] public float volume;

        public SpriterSound()
        {
            folder = -1;
            file = -1;
            panning = 0f;
            volume = 1.0f;
        }
    }

    public class CharacterMap : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
        [XmlElement("map")] public List<MapInstruction> maps = new List<MapInstruction>(); // <map> tags
    }

    public class MapInstruction
    {
        public MapInstruction() { target_folder = -1; target_file = -1; }

        [XmlAttribute] public int folder { get; set; }
        [XmlAttribute] public int file { get; set; }
        [XmlAttribute] public int target_folder { get; set; }
        [XmlAttribute] public int target_file { get; set; }
    }

    public class Animation : ScmlElement
    {
        public Animation()
        {
            looping = true;
        }

        private string _name;

        [XmlAttribute]
        public string name
        {
            get { return _name; }
            set { _name = AnimationNameSanitizer.Sanitize(value); }
        }

        private float _length;

        [XmlAttribute]
        public float length
        {
            get { return _length; }
            set { _length = value * 0.001f; }
        }

        [XmlAttribute] public bool looping { get; set; } // enum : NO_LOOPING,LOOPING //Dengar.NOTE: the actual values are true and false, so it's a bool
        [XmlArray("mainline"), XmlArrayItem("key")]
        public List<MainlineKey> mainlineKeys = new List<MainlineKey>(); // <key> tags within a single <mainline> tag
        [XmlElement("timeline")] public List<Timeline> timelines = new List<Timeline>(); // <timeline> tags
        [XmlElement("eventline")] public List<Eventline> eventlines = new List<Eventline>();
        [XmlElement("soundline")] public List<Soundline> soundlines = new List<Soundline>();
        [XmlElement("meta")] public Metadata metadata;
    }

    public class MainlineKey : SpriterKey
    {
        public override string ToString()
        {
            return $"{nameof(MainlineKey)}, id:{id}, time:{time}, curve_type:{curve_type}, " +
                $"c1:{c1} c2:{c2}, c3:{c3}, c4:{c4}";
        }

        public MainlineKey Clone()
        {
            var clone = (MainlineKey)MemberwiseClone();

            clone.boneRefs = new List<Ref>(boneRefs);
            clone.objectRefs = new List<Ref>(objectRefs);

            return clone;
        }

        [XmlElement("bone_ref")] public List<Ref> boneRefs = new List<Ref>(); // <bone_ref> tags
        [XmlElement("object_ref")] public List<Ref> objectRefs = new List<Ref>(); // <object_ref> tags
    }

    public class Ref : ScmlElement
    {
        public Ref() { parent = -1; }

        [XmlAttribute] public int parent { get; set; } // -1==no parent - uses ScmlObject spatialInfo as parentInfo
        [XmlAttribute] public int timeline { get; set; } //Dengar.NOTE: Again, the above comment is an artifact from the pseudocode
        [XmlAttribute] public int key { get; set; }     //However, the fact that -1 equals "no parent" does come in useful later
        private float z;
        [XmlAttribute]
        public float z_index
        { //Translate Sprite's Z-index in something we can use in Unity
            get { return z; }               //I choose to use position_z instead of order in layer because there are just potentially way too many
            set { z = value * -0.001f; }    //body parts to work with. This way the order in layer is reserved for entire Spriter entities
        }
    }

    public enum ObjectType
    {
        sprite,
        bone,
        box,
        point,
        sound,
        entity,
        variable,
        [XmlEnum("event")] spriterEvent // Did older Spriter files have this?  Seems to be used only for <obj_info> tags.
    }

    public class Timeline : ScmlElement
    {
        [XmlAttribute] public string name { get; set; }
        [XmlAttribute("object_type")] public ObjectType objectType { get; set; } // enum : SPRITE,BONE,BOX,POINT,SOUND,ENTITY,VARIABLE //Dengar.NOTE (except not in all caps)
        [XmlElement("key")] public List<TimelineKey> keys = new List<TimelineKey>(); // <key> tags within <timeline> tags
        [XmlElement("meta")] public Metadata metadata;
    }

    public enum CurveType
    {
        linear,
        instant,
        quadratic,
        cubic,
        quartic,
        quintic,
        bezier
    }

    public class TimelineKey : SpriterKey
    {
        public TimelineKey() { spin = 1; }

        public override string ToString()
        {
            return $"{nameof(TimelineKey)}, id:{id}, time_s:{time_s}, spin:{spin}, curve_type:{curve_type}, " +
                $"c1:{c1} c2:{c2}, c3:{c3}, c4:{c4}, info:{info}";
        }

        public TimelineKey Clone()
        {
            var clone = (TimelineKey)MemberwiseClone();

            clone.info = info.Clone();
            clone.timeZeroAuxKey = timeZeroAuxKey?.Clone();

            return clone;
        }

        [XmlAttribute] public int spin { get; set; }

        [XmlElement("bone", typeof(SpatialInfo)), XmlElement("object", typeof(SpriteInfo))]
        public SpatialInfo info { get; set; }

        [XmlIgnore] public TimelineKey timeZeroAuxKey;
    }

    public class SpatialInfo
    {
        public SpatialInfo()
        {
            x = 0;
            y = 0;
            angle = 0;
            scale_x = 1;
            scale_y = 1;
            trueScaleX = float.NaN;
            trueScaleY = float.NaN;
            a = 1;
            parentBoneName = "Unknown";
        }

        public override string ToString()
        {
            return $"{nameof(SpatialInfo)}: x:{x}, y:{y}, angle:{angle}, scale_x:{scale_x}, scale_y:{scale_y}, " +
                $"a(alpha):{a}, parentBoneName:{parentBoneName}";
        }

        public virtual SpatialInfo Clone()
        {
            return (SpatialInfo)MemberwiseClone();
        }

        // parentBoneName will be set appropriately once the data is loaded and processed.
        public string parentBoneName { get; set; }

        private float _x;

        [XmlAttribute]
        public float x
        {
            get { return _x; }
            set
            {
                if (ScmlImportOptions.options != null)
                {
                    _x = value * (1f / ScmlImportOptions.options.pixelsPerUnit); // Convert Spriter space into Unity space using pixelsPerUnit
                }
                else
                {
                    _x = value * 0.01f;
                }
            }
        }

        private float _y;

        [XmlAttribute]
        public float y
        {
            get { return _y; }
            set
            {
                if (ScmlImportOptions.options != null)
                {
                    _y = value * (1f / ScmlImportOptions.options.pixelsPerUnit); // Convert Spriter space into Unity space using pixelsPerUnit
                }
                else
                {
                    _y = value * 0.01f;
                }
            }
        }

        [XmlAttribute] public float angle { get; set; }

        private float sx;

        [XmlAttribute]
        public float scale_x
        {
            get { return sx; }
            set
            {
                sx = value;
                if (float.IsNaN(trueScaleX)) trueScaleX = value;
            }
        }

        private float trueScaleX;
        private float sy;

        [XmlAttribute]
        public float scale_y
        {
            get { return sy; }
            set
            {
                sy = value;
                if (float.IsNaN(trueScaleY)) trueScaleY = value;
            }
        }

        private float trueScaleY;

        [XmlAttribute] public float a { get; set; } // Alpha

        public float z_index { get; set;  } // This is the value from the mainlineKey.objectInfo or mainlineKey.boneInfo.
        public int SortingOrder { get { return ZIndexToSortingOrder(z_index); } }

        public static int ZIndexToSortingOrder(float zIndex)
        {
            return Mathf.RoundToInt(-10000f * zIndex); // If you change this then change SortingOrderUpdater also.
        }

        public bool processed = false;

        //Some very funky maths to make sure all the scale values are off the bones and on the sprite instead
        public bool Process(SpatialInfo parent)
        {
            if (GetType() == typeof(SpatialInfo))
            {
                scale_x = (scale_x > 0) ? 1 : -1;
                scale_y = (scale_y > 0) ? 1 : -1;
                trueScaleX = Mathf.Abs(trueScaleX);
                trueScaleY = Mathf.Abs(trueScaleY);

                if (parent != null)
                {
                    if (!float.IsNaN(parent.trueScaleX))
                    {
                        _x *= parent.trueScaleX;
                        trueScaleX *= parent.trueScaleX;
                    }
                    if (!float.IsNaN(parent.trueScaleY))
                    {
                        _y *= parent.trueScaleY;
                        trueScaleY *= parent.trueScaleY;
                    }
                }

                return processed = true;
            }

            if (parent != null)
            {
                if (!float.IsNaN(parent.trueScaleX))
                {
                    _x *= parent.trueScaleX;
                    scale_x *= parent.trueScaleX;
                }
                if (!float.IsNaN(parent.trueScaleY))
                {
                    _y *= parent.trueScaleY;
                    scale_y *= parent.trueScaleY;
                }
            }

            return processed = true;
        }
    }

    public class SpriteInfo : SpatialInfo
    {
        public SpriteInfo() : base()
        {
            // These will be set appropriately once the data is loaded and processed.
            pivot_x = float.NaN;
            pivot_y = float.NaN;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, {nameof(SpriteInfo)}: folder: {folder}, file: {file}, " +
                $"pivot_x:{pivot_x}, pivot_y:{pivot_y}, " +
                $"default_pivot_x:{default_pivot_x}, default_pivot_y:{default_pivot_y}, " +
                $"IsDefaultPivots:{IsDefaultPivots}";
        }

        public override SpatialInfo Clone()
        {
            return (SpatialInfo)MemberwiseClone();
        }

        [XmlAttribute] public int folder { get; set; }
        [XmlAttribute] public int file { get; set; }

        [XmlAttribute] public float pivot_x { get; set; } // Pivot read from SCML file or file defaults if not read.
        [XmlAttribute] public float pivot_y { get; set; }

        public float default_pivot_x { get; set; } // Imported file's pivots.
        public float default_pivot_y { get; set; }

        public void InitPivots(File spriteFile)
        {
            if (spriteFile != null)
            {
                default_pivot_x = spriteFile.pivot_x; // These are the import pivots.
                default_pivot_y = spriteFile.pivot_y;

                // If a value wasn't read from the SCML file then use the file's pivots.
                if (float.IsNaN(pivot_x) || float.IsNaN(pivot_y))
                {
                    pivot_x = spriteFile.pivot_x;
                    pivot_y = spriteFile.pivot_y;
                }
            }
        }

        public bool IsDefaultPivots
        {
            get { return pivot_x == default_pivot_x && pivot_y == default_pivot_y; }
        }
    }

    public abstract class ScmlElement
    {
        [XmlAttribute] public int id { get; set; }
    }

    public static class EntityNameSanitizer
    {
        private static readonly char[] InvalidChars =
        {
            '/',
            '\\',
            '<',
            '>',
            ':',
            '"',
            '|',
            '?',
            '*'
        };

        private static readonly string WarningMsg =
            "Spriter2UnityDX: The Spriter entity name '{0}' contains one or more characters that are invalid for " +
            "prefab filenames and animation controller filenames.  It has been renamed to '{1}'.  Change the " +
            "entity name in Spriter to avoid this warning.";

        /// <summary>
        /// Scans the provided entity name for invalid characters.  An animation controller
        /// file and a prefab file will be created with this name so it can't contain any
        /// characters that are invalid for filenames.
        /// </summary>
        /// <returns>
        /// A warning will be logged if the name is invalid, in which case a sanitized
        /// string will be returned.  The original string will be returned if it is valid.
        /// </returns>
        public static string Sanitize(string entityName)
        {
            return NameSanitizer.Sanitize(entityName, InvalidChars, WarningMsg);
        }
    }

    public static class AnimationNameSanitizer
    {
        private static readonly char[] InvalidChars =
        {
            '.', // This chararter is invalid for animation controller state names.
            '/',
            '\\',
            '<',
            '>',
            ':',
            '"',
            '|',
            '?',
            '*'
        };

        private static readonly string WarningMsg =
            "Spriter2UnityDX: The Spriter animation name '{0}' contains one or more characters that are invalid for " +
            "Unity animation state names and/or animation clip filenames.  It has been renamed to '{1}'.  Change the " +
            "animation name in Spriter to avoid this warning.";

        /// <summary>
        /// Scans the provided animation name for invalid characters.  An animation
        /// clip file will be created with this name so it can't contain any characters that
        /// are invalid for filenames.  Also, the animation controller doesn't allow for '.'
        /// or '/'.
        /// </summary>
        /// <returns>
        /// A warning will be logged if the name is invalid, in which case a sanitized
        /// string will be returned.  The original string will be returned if it is valid.
        /// </returns>
        public static string Sanitize(string animationName)
        {
            return NameSanitizer.Sanitize(animationName, InvalidChars, WarningMsg);
        }
    }

    public static class NameSanitizer
    {
        public static string Sanitize(string name, char[] invalidChars, string warningMsg)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            var original = name;
            var sb = new StringBuilder(name);

            bool wasSanitized = false;

            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];

                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    sb[i] = '_';
                    wasSanitized = true;
                }
            }

            if (wasSanitized)
            {
                Debug.LogWarningFormat(warningMsg, original, sb.ToString());
            }

            return sb.ToString();
        }
    }
}
