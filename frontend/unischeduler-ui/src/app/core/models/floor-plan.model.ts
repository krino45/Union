export enum FloorPlanNodeType {
  Room = 0,
  Staircase = 1,
  Elevator = 2,
  Entrance = 3,
  Corridor = 4
}

export interface FloorPlanNode {
  id: string;
  buildingId: string;
  floor: number;
  x: number;
  y: number;
  nodeType: FloorPlanNodeType;
  roomId: string | null;
  label: string | null;
}

export interface FloorPlanEdge {
  id: string;
  buildingId: string;
  fromNodeId: string;
  toNodeId: string;
  distanceMeters: number;
}

export interface FloorPlan {
  buildingId: string;
  nodes: FloorPlanNode[];
  edges: FloorPlanEdge[];
}

export interface SaveFloorPlanRequest {
  nodes: SaveFloorPlanNodeRequest[];
  edges: SaveFloorPlanEdgeRequest[];
}

export interface SaveFloorPlanNodeRequest {
  id: string;
  floor: number;
  x: number;
  y: number;
  nodeType: FloorPlanNodeType;
  roomId: string | null;
  label: string | null;
}

export interface SaveFloorPlanEdgeRequest {
  fromNodeId: string;
  toNodeId: string;
  distanceMeters: number;
}
