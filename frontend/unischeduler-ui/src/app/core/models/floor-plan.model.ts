export enum FloorPlanNodeType {
  Room      = 'Room',
  Staircase = 'Staircase',
  Elevator  = 'Elevator',
  Entrance  = 'Entrance',
  Corridor  = 'Corridor',
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

export interface FloorPlanDraftSummary {
  id: string;
  buildingId: string;
  name: string;
  isOpenToAdmins: boolean;
  lastModified: string;
  ownerUserId: string;
  ownerUsername: string;
  isMine: boolean;
}

export interface FloorPlanDraft extends FloorPlanDraftSummary {
  draftJson: string;
}

export interface FloorPlanSummary {
  id: string;
  buildingId: string;
  name: string;
  isActive: boolean;
  createdAt: string;
  createdByUserId: string | null;
  createdByUsername: string | null;
}
