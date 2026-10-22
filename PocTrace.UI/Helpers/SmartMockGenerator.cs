using System;
using System.Collections.Generic;

using Bogus;

using PocTrace.Core.Enums;
using PocTrace.Core.Models;

namespace PocTrace.UI.Helpers
{
    public class SmartMockGenerator
    {
        public class MockGraphData
        {
            public List<NodeInfo> Nodes { get; set; }
            public List<PocData> Pocs { get; set; }
            public List<PipeData> Pipes { get; set; }
        }

        public MockGraphData GenerateMassiveGraph(int equipmentCount)
        {
            return null;
            /*
            var nodes = new List<NodeInfo>();
            var pocs = new List<PocData>();
            var pipes = new List<PipeData>();
            var faker = new Faker();

            // 1. 장비 생성
            for (int i = 0; i < equipmentCount; i++)
            {
                var equipId = Guid.NewGuid();
                var tag = $"TEST-TK-{i:0000}"; // 태그를 예측 가능하게 변경
                nodes.Add(new NodeInfo(equipId, tag, $"Mock Tank {tag}", NodeType.Equipment));

                // 2. 장비당 POC 및 연결 생성
                int pocCount = 3;
                for (int j = 0; j < pocCount; j++)
                {
                    // 단순화: 1번 POC는 노즐로 직결 (Happy Path)
                    if (j == 0)
                    {
                        var nozzleId = Guid.NewGuid();
                        var nozzleTag = $"{tag}-N01";
                        nodes.Add(new NodeInfo(nozzleId, nozzleTag, "Nozzle", NodeType.Nozzle));

                        var nozzlePocId = Guid.NewGuid();
                        //pocs.Add(new PocData(nozzlePocId, nozzleId, null, nozzleTag));

                        // 장비 -> 노즐 연결
                        //pocs.Add(new PocData(Guid.NewGuid(), equipId, nozzlePocId, nozzleTag));
                    }
                    else
                    {
                        // 나머지는 빈 연결 (Empty)
                        //pocs.Add(new PocData(Guid.NewGuid(), equipId, null, "Spare"));
                    }
                }
            }

            return new MockGraphData { Nodes = nodes, Pocs = pocs, Pipes = pipes };
            */
        }
    }
}