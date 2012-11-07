﻿using System;
using System.IO;
using NUnit.Framework;
using SisoDb.Serialization;
using SisoDb.Structures.Schemas;

namespace SisoDb.UnitTests.Serialization
{
    [TestFixture]
    public class InternalJsonSerializerTests : UnitTestBase
    {
        private ISisoDbSerializer _sisoDbSerializer;

        protected override void OnFixtureInitialize()
        {
            base.OnFixtureInitialize();

            var structureTypeFactory = new StructureTypeFactory();
            _sisoDbSerializer = new InternalJsonSerializer(structureTypeFactory.Configurations.GetConfiguration);
        }

        [Test]
        public void Serialize_WhenNullEntity_ReturnsEmptyString()
        {
            var json = _sisoDbSerializer.Serialize<JsonEntity>(null);

            Assert.AreEqual(string.Empty, json);
        }

        [Test]
        public void Serialize_WhenOnlyPrivateGetterAndStructureIdExists_ReturnsJsonWithStructureId()
        {
            var entity = new JsonEntityWithPrivateGetter { Name = "Daniel" };

            var json = _sisoDbSerializer.Serialize(entity);

            Assert.AreEqual("{\"StructureId\":0}", json);
        }

        [Test]
        public void Serialize_WhenPrivateSetterExists_IsSerialized()
        {
            var entity = new JsonEntity();
            entity.SetName("Daniel");

            var json = _sisoDbSerializer.Serialize(entity);

            Assert.AreEqual("{\"StructureId\":0,\"Name\":\"Daniel\"}", json);
        }

        [Test]
        public void Deserialize_WhenPrivateSetterExists_IsDeserialized()
        {
            var json = @"{""Name"":""Daniel""}";

            var entity = _sisoDbSerializer.Deserialize<JsonEntity>(json);

            Assert.AreEqual("Daniel", entity.Name);
        }

        [Test]
        public void Serialize_WhenItemIsYButSerializedAsX_AllYMembersAreSerialized()
        {
            var y = new JsonEntityY { Int1 = 42, String1 = "The String1", Data = new MemoryStream(BitConverter.GetBytes(333)).ToArray() };

            var json = _sisoDbSerializer.Serialize<JsonEntityX>(y);

			Assert.AreEqual("{\"Data\":\"TQEAAA==\",\"StructureId\":0,\"String1\":\"The String1\",\"Int1\":42}", json);
        }

        [Test]
        public void Serialize_WhenReferencingOtherStructure_ReferencedStructureIsRepresentedInJson()
        {
            var structure = new Structure
            {
                ReferencedStructureId = 999,
                ReferencedStructure = {OtherStructureString = "Not to be included"},
                Item = {String1 = "To be included"}
            };

            var json = _sisoDbSerializer.Serialize(structure);

            const string expectedJson = "{\"StructureId\":0,\"ReferencedStructureId\":999,\"ReferencedStructure\":{\"StructureId\":999,\"OtherStructureString\":\"Not to be included\"},\"Item\":{\"String1\":\"To be included\",\"Int1\":0}}";
            Assert.AreEqual(expectedJson, json);
        }

        private class JsonEntity
        {
            public int StructureId { get; set; }
            public string Name { get; private set; }

            public void SetName(string name)
            {
                Name = name;
            }
        }

        private class JsonEntityWithPrivateGetter
        {
            public int StructureId { get; set; }
            public string Name { private get; set; }
        }

        private class JsonEntityX
        {
            public int StructureId { get; set; }
            public string String1 { get; set; }
            public int Int1 { get; set; }
        }

        private class JsonEntityY : JsonEntityX
        {
            public byte[] Data { get; set; }
        }

        private class Item
        {
            public string String1 { get; set; }
            public int Int1 { get; set; }
            public DateTime? DateTime1 { get; set; }
        }

        private class Structure
        {
            public int StructureId { get; set; }
            public int ReferencedStructureId 
            {
                get { return ReferencedStructure.StructureId; }
                set { ReferencedStructure.StructureId = value; }
            }

            public OtherStructure ReferencedStructure { get; set; }
            public Item Item { get; set; }
            public Structure()
            {
                ReferencedStructure = new OtherStructure();
                Item = new Item();
            }
        }

        private class OtherStructure
        {
            public int StructureId { get; set; }
            public string OtherStructureString { get; set; }
        }
    }
}