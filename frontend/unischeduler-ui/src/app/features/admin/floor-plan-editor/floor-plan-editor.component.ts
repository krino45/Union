import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../../core/services/api.service';
import { Building, Room, FloorPlan, FloorPlanNode, FloorPlanEdge, FloorPlanNodeType } from '../../../core/models';

type EditorMode = 'select' | 'place' | 'edge' | 'delete';

interface EditorNode extends FloorPlanNode {
  selected: boolean;
}

interface EdgeSource {
  nodeId: string;
}

const CANVAS_W = 900;
const CANVAS_H = 650;
const NODE_RADIUS = 22;

const NODE_COLORS: Record<FloorPlanNodeType, string> = {
  [FloorPlanNodeType.Room]:      '#1565c0',
  [FloorPlanNodeType.Staircase]: '#e65100',
  [FloorPlanNodeType.Elevator]:  '#6a1b9a',
  [FloorPlanNodeType.Entrance]:  '#2e7d32',
  [FloorPlanNodeType.Corridor]:  '#757575',
};

const NODE_LABELS_RU: Record<FloorPlanNodeType, string> = {
  [FloorPlanNodeType.Room]:      'Аудитория',
  [FloorPlanNodeType.Staircase]: 'Лестница',
  [FloorPlanNodeType.Elevator]:  'Лифт',
  [FloorPlanNodeType.Entrance]:  'Вход',
  [FloorPlanNodeType.Corridor]:  'Коридор',
};

@Component({
  selector: 'app-floor-plan-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatButtonModule, MatIconModule, MatSelectModule,
    MatFormFieldModule, MatInputModule, MatSnackBarModule, MatTooltipModule,
    MatDialogModule, MatProgressSpinnerModule
  ],
  template: `
<div class="editor-page">
  <div class="page-header">
    <h1>Редактор планировок</h1>
    <div class="header-controls">
      <mat-form-field appearance="outline" class="building-select">
        <mat-label>Корпус</mat-label>
        <mat-select [(ngModel)]="selectedBuildingId" (ngModelChange)="onBuildingChange($event)"
                    [ngModelOptions]="{standalone: true}">
          <mat-option *ngFor="let b of buildings" [value]="b.id">
            {{ b.shortCode }} — {{ b.address }}
          </mat-option>
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" class="scale-field">
        <mat-label>Масштаб (м/100px)</mat-label>
        <input matInput type="number" [(ngModel)]="scale" [ngModelOptions]="{standalone: true}"
               min="0.1" step="0.5" (change)="onScaleChange()">
      </mat-form-field>
    </div>
  </div>

  <div class="editor-container" *ngIf="selectedBuilding; else noBuilding">
    <!-- Floor tabs -->
    <div class="floor-tabs">
      <button *ngFor="let f of floors"
              class="floor-tab"
              [class.active]="f === currentFloor"
              (click)="selectFloor(f)">
        {{ floorLabel(f) }}
      </button>
    </div>

    <!-- Toolbar -->
    <mat-card class="toolbar-card">
      <div class="toolbar">
        <span class="toolbar-label">Режим:</span>
        <button mat-stroked-button [class.active-tool]="mode === 'select'" (click)="setMode('select')" matTooltip="Выбор/перемещение">
          <mat-icon>mouse</mat-icon> Выбор
        </button>
        <button mat-stroked-button [class.active-tool]="mode === 'place'" (click)="setMode('place')" matTooltip="Разместить узел">
          <mat-icon>add_circle</mat-icon> Добавить
        </button>
        <button mat-stroked-button [class.active-tool]="mode === 'edge'" (click)="setMode('edge')" matTooltip="Соединить узлы">
          <mat-icon>timeline</mat-icon> Путь
        </button>
        <button mat-stroked-button [class.active-tool]="mode === 'delete'" (click)="setMode('delete')" matTooltip="Удалить">
          <mat-icon>delete</mat-icon> Удалить
        </button>
        <span class="toolbar-sep" *ngIf="mode === 'place'">|</span>
        <ng-container *ngIf="mode === 'place'">
          <button *ngFor="let nt of nodeTypes" mat-stroked-button
                  [class.active-tool]="placeType === nt.type"
                  (click)="placeType = nt.type"
                  [style.border-color]="nt.color">
            <span [style.color]="nt.color">{{ nt.label }}</span>
          </button>
        </ng-container>
        <span *ngIf="mode === 'edge' && edgeSource" class="edge-hint">
          <mat-icon>arrow_forward</mat-icon> Нажмите на второй узел
        </span>
        <div class="toolbar-spacer"></div>
        <button mat-raised-button color="primary" (click)="save()" [disabled]="saving || !selectedBuildingId">
          <mat-icon>save</mat-icon> Сохранить
        </button>
        <button mat-button (click)="reload()" [disabled]="loading">
          <mat-icon>refresh</mat-icon>
        </button>
      </div>
    </mat-card>

    <!-- Main canvas + properties panel -->
    <div class="canvas-area">
      <div class="canvas-wrapper" (mousemove)="onMouseMove($event)" (mouseup)="onMouseUp($event)">
        <svg #svgCanvas
             [attr.width]="canvasW" [attr.height]="canvasH"
             class="floor-canvas"
             (mousedown)="onCanvasMouseDown($event)">

          <!-- Grid -->
          <defs>
            <pattern id="grid" width="50" height="50" patternUnits="userSpaceOnUse">
              <path d="M 50 0 L 0 0 0 50" fill="none" stroke="#e8e8e8" stroke-width="1"/>
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#grid)" pointer-events="none"/>
          <rect width="100%" height="100%" fill="none" stroke="#bbb" stroke-width="1" pointer-events="none"/>

          <!-- Edges -->
          <g *ngFor="let e of currentFloorEdges()">
            <line
              [attr.x1]="nodeById(e.fromNodeId)?.x ?? 0"
              [attr.y1]="nodeById(e.fromNodeId)?.y ?? 0"
              [attr.x2]="nodeById(e.toNodeId)?.x ?? 0"
              [attr.y2]="nodeById(e.toNodeId)?.y ?? 0"
              [class.cross-floor]="isEdgeCrossFloor(e)"
              class="edge-line"
              [class.selected-edge]="selectedEdgeId === e.id"
              (mousedown)="onEdgeMouseDown($event, e)"/>
            <text
              [attr.x]="edgeMidX(e)"
              [attr.y]="edgeMidY(e) - 6"
              class="edge-label"
              text-anchor="middle">
              {{ e.distanceMeters }}м
            </text>
          </g>

          <!-- Edge-in-progress line -->
          <line *ngIf="edgeSource && mousePos"
                [attr.x1]="nodeById(edgeSource.nodeId)?.x ?? 0"
                [attr.y1]="nodeById(edgeSource.nodeId)?.y ?? 0"
                [attr.x2]="mousePos.x" [attr.y2]="mousePos.y"
                class="edge-preview"/>

          <!-- Nodes -->
          <g *ngFor="let node of currentFloorNodes()"
             [attr.transform]="'translate(' + node.x + ',' + node.y + ')'"
             class="node-group"
             [class.selected-node]="node.selected"
             (mousedown)="onNodeMouseDown($event, node)">
            <circle [attr.r]="NODE_RADIUS"
                    [attr.fill]="nodeColor(node)"
                    [attr.stroke]="node.selected ? '#ff6f00' : '#fff'"
                    stroke-width="2.5"/>
            <text text-anchor="middle" dy="0.35em" class="node-icon">
              {{ nodeIcon(node) }}
            </text>
            <text text-anchor="middle" dy="2.2em" class="node-room-label">
              {{ nodeDisplayLabel(node) }}
            </text>
            <!-- Cross-floor connection indicator -->
            <circle *ngIf="hasCrossFloorEdge(node)" cx="16" cy="-16" r="6" fill="#ff6f00" stroke="#fff" stroke-width="1"/>
            <text *ngIf="hasCrossFloorEdge(node)" x="16" y="-12" text-anchor="middle" font-size="8" fill="#fff">↕</text>
          </g>
        </svg>
      </div>

      <!-- Properties panel -->
      <mat-card class="props-panel" *ngIf="selectedNode || selectedEdgeId">
        <ng-container *ngIf="selectedNode">
          <h3>Узел</h3>
          <div class="prop-row">
            <span class="prop-label">Тип:</span>
            <mat-select [(ngModel)]="selectedNode.nodeType" [ngModelOptions]="{standalone: true}"
                        (ngModelChange)="onNodeTypeChange(selectedNode)">
              <mat-option *ngFor="let nt of nodeTypes" [value]="nt.type">
                <span [style.color]="nt.color">{{ nt.label }}</span>
              </mat-option>
            </mat-select>
          </div>
          <div class="prop-row">
            <span class="prop-label">Этаж:</span>
            <input type="number" [(ngModel)]="selectedNode.floor" [ngModelOptions]="{standalone: true}"
                   class="prop-input" (change)="markDirty()">
          </div>
          <div class="prop-row" *ngIf="selectedNode.nodeType === FloorPlanNodeType.Room">
            <span class="prop-label">Аудитория:</span>
            <mat-select [(ngModel)]="selectedNode.roomId" [ngModelOptions]="{standalone: true}"
                        (ngModelChange)="onRoomAssigned(selectedNode, $event)" class="room-select">
              <mat-option [value]="null">— не привязана —</mat-option>
              <mat-option *ngFor="let r of buildingRooms" [value]="r.id">
                {{ r.number }} (эт. {{ r.floor }})
              </mat-option>
            </mat-select>
          </div>
          <div class="prop-row">
            <span class="prop-label">Метка:</span>
            <input type="text" [(ngModel)]="selectedNode.label" [ngModelOptions]="{standalone: true}"
                   class="prop-input" placeholder="Необязательно" (input)="markDirty()">
          </div>
          <div class="prop-row">
            <span class="prop-label">X / Y:</span>
            <span class="prop-val">{{ selectedNode.x | number:'1.0-0' }} / {{ selectedNode.y | number:'1.0-0' }}</span>
          </div>
          <button mat-stroked-button color="warn" (click)="deleteNode(selectedNode)" class="delete-btn">
            <mat-icon>delete</mat-icon> Удалить узел
          </button>
        </ng-container>

        <ng-container *ngIf="selectedEdgeId && !selectedNode">
          <h3>Путь</h3>
          <ng-container *ngIf="selectedEdge() as edge">
            <div class="prop-row">
              <span class="prop-label">Расстояние (м):</span>
              <input type="number" [(ngModel)]="edge.distanceMeters" [ngModelOptions]="{standalone: true}"
                     class="prop-input" min="1" (change)="markDirty()">
            </div>
            <div class="prop-row">
              <span class="prop-label">Этажи:</span>
              <span class="prop-val">
                {{ nodeById(edge.fromNodeId)?.floor }} → {{ nodeById(edge.toNodeId)?.floor }}
              </span>
            </div>
            <button mat-stroked-button color="warn" (click)="deleteEdge(edge)" class="delete-btn">
              <mat-icon>delete</mat-icon> Удалить путь
            </button>
          </ng-container>
        </ng-container>
      </mat-card>

      <mat-card class="props-panel hint-panel" *ngIf="!selectedNode && !selectedEdgeId">
        <div class="hint-content">
          <mat-icon class="hint-icon">info</mat-icon>
          <div class="hint-texts">
            <p *ngIf="mode === 'select'">Нажмите на узел или путь чтобы выбрать. Перетащите узел для перемещения.</p>
            <p *ngIf="mode === 'place'">Выберите тип и нажмите на холст для добавления узла. Узлы типа «Аудитория» нужно привязать к аудитории в панели свойств.</p>
            <p *ngIf="mode === 'edge'">Нажмите первый узел, затем второй — между ними создастся путь. Расстояние рассчитывается автоматически.</p>
            <p *ngIf="mode === 'delete'">Нажмите на узел или путь для удаления.</p>
          </div>
        </div>
        <div class="legend">
          <div *ngFor="let nt of nodeTypes" class="legend-item">
            <span class="legend-dot" [style.background]="nt.color"></span>
            {{ nt.label }}
          </div>
        </div>
      </mat-card>
    </div>

    <div class="loading-overlay" *ngIf="loading">
      <mat-spinner diameter="48"></mat-spinner>
    </div>
  </div>

  <ng-template #noBuilding>
    <mat-card class="no-building-card">
      <mat-icon>apartment</mat-icon>
      <p>Выберите корпус для редактирования планировки</p>
    </mat-card>
  </ng-template>
</div>
  `,
  styles: [`
    .editor-page { padding: 0; }
    .page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
    .page-header h1 { margin: 0; }
    .header-controls { display: flex; gap: 12px; align-items: center; }
    .building-select { min-width: 280px; }
    .scale-field { width: 160px; }

    .floor-tabs { display: flex; gap: 4px; margin-bottom: 8px; flex-wrap: wrap; }
    .floor-tab {
      padding: 4px 14px; border: 1px solid #bbb; border-radius: 16px;
      background: #fff; cursor: pointer; font-size: 13px; transition: all 0.15s;
    }
    .floor-tab:hover { background: #e3f2fd; border-color: #1565c0; }
    .floor-tab.active { background: #1565c0; color: #fff; border-color: #1565c0; font-weight: 600; }

    .toolbar-card { margin-bottom: 8px; padding: 0; }
    .toolbar-card .mat-mdc-card-content { padding: 0; }
    .toolbar {
      display: flex; align-items: center; gap: 8px; padding: 8px 12px;
      flex-wrap: wrap;
    }
    .toolbar-label { font-size: 13px; color: #666; font-weight: 600; }
    .toolbar-sep { color: #bbb; }
    .toolbar-spacer { flex: 1; }
    .active-tool { background: #e3f2fd !important; border-color: #1565c0 !important; }
    .edge-hint { display: flex; align-items: center; gap: 4px; font-size: 13px; color: #e65100; font-weight: 600; }

    .canvas-area { display: flex; gap: 12px; align-items: flex-start; position: relative; }
    .canvas-wrapper { flex-shrink: 0; }
    .floor-canvas {
      border: 1px solid #ddd; border-radius: 4px; background: #fafafa;
      cursor: crosshair; user-select: none;
    }

    .edge-line { stroke: #90a4ae; stroke-width: 2.5; cursor: pointer; }
    .edge-line:hover { stroke: #455a64; stroke-width: 3.5; }
    .edge-line.selected-edge { stroke: #ff6f00; stroke-width: 3; }
    .edge-line.cross-floor { stroke-dasharray: 6 4; stroke: #7986cb; }
    .edge-preview { stroke: #ff8a65; stroke-width: 2; stroke-dasharray: 5 3; pointer-events: none; }
    .edge-label { font-size: 11px; fill: #546e7a; pointer-events: none; }

    .node-group { cursor: pointer; }
    .node-group:hover circle { filter: brightness(1.15); }
    .node-icon { font-size: 12px; fill: #fff; pointer-events: none; font-family: monospace; font-weight: bold; }
    .node-room-label { font-size: 10px; fill: #37474f; pointer-events: none; }
    .selected-node circle { filter: drop-shadow(0 0 4px #ff6f00); }

    .props-panel { width: 240px; flex-shrink: 0; padding: 12px !important; }
    .props-panel h3 { margin: 0 0 12px; font-size: 14px; }
    .prop-row { display: flex; flex-direction: column; margin-bottom: 10px; gap: 4px; }
    .prop-label { font-size: 11px; color: #666; font-weight: 600; text-transform: uppercase; letter-spacing: 0.3px; }
    .prop-val { font-size: 13px; color: #333; }
    .prop-input { border: 1px solid #ccc; border-radius: 4px; padding: 4px 6px; font-size: 13px; width: 100%; }
    .room-select { font-size: 13px; }
    .delete-btn { margin-top: 8px; width: 100%; }
    .hint-panel { background: #f9fbe7 !important; }
    .hint-content { display: flex; gap: 8px; align-items: flex-start; margin-bottom: 12px; }
    .hint-icon { color: #afb42b; font-size: 20px; flex-shrink: 0; }
    .hint-texts p { margin: 0; font-size: 13px; color: #555; }
    .legend { display: flex; flex-direction: column; gap: 4px; }
    .legend-item { display: flex; align-items: center; gap: 6px; font-size: 12px; }
    .legend-dot { width: 12px; height: 12px; border-radius: 50%; flex-shrink: 0; }

    .editor-container { position: relative; }
    .loading-overlay {
      position: absolute; inset: 0; background: rgba(255,255,255,0.7);
      display: flex; align-items: center; justify-content: center; z-index: 10;
    }
    .no-building-card {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      gap: 16px; padding: 64px; color: #9e9e9e; font-size: 16px;
    }
    .no-building-card mat-icon { font-size: 64px; height: 64px; width: 64px; }
  `]
})
export class FloorPlanEditorComponent implements OnInit {
  @ViewChild('svgCanvas') svgCanvas!: ElementRef<SVGElement>;

  buildings: Building[] = [];
  selectedBuildingId: string | null = null;
  selectedBuilding: Building | null = null;
  buildingRooms: Room[] = [];

  nodes: EditorNode[] = [];
  edges: FloorPlanEdge[] = [];
  floors: number[] = [];
  currentFloor = 1;
  scale = 5; // meters per 100px
  dirty = false;
  loading = false;
  saving = false;

  mode: EditorMode = 'select';
  placeType: FloorPlanNodeType = FloorPlanNodeType.Room;
  edgeSource: EdgeSource | null = null;
  selectedEdgeId: string | null = null;
  mousePos: { x: number; y: number } | null = null;

  // Drag state
  private dragging: EditorNode | null = null;
  private dragOffset = { x: 0, y: 0 };

  readonly NODE_RADIUS = NODE_RADIUS;
  readonly CANVAS_W = CANVAS_W;
  readonly CANVAS_H = CANVAS_H;
  readonly FloorPlanNodeType = FloorPlanNodeType;
  readonly canvasW = CANVAS_W;
  readonly canvasH = CANVAS_H;

  readonly nodeTypes = [
    { type: FloorPlanNodeType.Room,      label: 'Аудитория', color: NODE_COLORS[FloorPlanNodeType.Room]      },
    { type: FloorPlanNodeType.Staircase, label: 'Лестница',  color: NODE_COLORS[FloorPlanNodeType.Staircase] },
    { type: FloorPlanNodeType.Elevator,  label: 'Лифт',      color: NODE_COLORS[FloorPlanNodeType.Elevator]  },
    { type: FloorPlanNodeType.Entrance,  label: 'Вход',      color: NODE_COLORS[FloorPlanNodeType.Entrance]  },
    { type: FloorPlanNodeType.Corridor,  label: 'Коридор',   color: NODE_COLORS[FloorPlanNodeType.Corridor]  },
  ];

  get selectedNode(): EditorNode | undefined {
    return this.nodes.find(n => n.selected);
  }

  constructor(private api: ApiService, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.api.getBuildings().subscribe(bs => { this.buildings = bs; });
  }

  onBuildingChange(id: string): void {
    this.selectedBuilding = this.buildings.find(b => b.id === id) ?? null;
    this.buildFloors();
    this.loadFloorPlan(id);
    this.api.getRooms({ buildingId: id }).subscribe(r => { this.buildingRooms = r; });
  }

  buildFloors(): void {
    if (!this.selectedBuilding) { this.floors = []; return; }
    const b = this.selectedBuilding;
    const result: number[] = [];
    for (let f = -b.numberOfBasementFloors; f <= b.numberOfFloors; f++) {
      if (f !== 0) result.push(f);
    }
    this.floors = result;
    this.currentFloor = result.includes(1) ? 1 : (result[0] ?? 1);
  }

  loadFloorPlan(id: string): void {
    this.loading = true;
    this.nodes = [];
    this.edges = [];
    this.clearSelection();
    this.api.getFloorPlan(id).subscribe({
      next: fp => {
        this.nodes = fp.nodes.map(n => ({ ...n, selected: false }));
        this.edges = fp.edges;
        this.dirty = false;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  reload(): void {
    if (this.selectedBuildingId) this.loadFloorPlan(this.selectedBuildingId);
  }

  selectFloor(f: number): void {
    this.currentFloor = f;
    this.clearSelection();
  }

  floorLabel(f: number): string {
    if (f < 0) return `Подвал ${f}`;
    return `Этаж ${f}`;
  }

  currentFloorNodes(): EditorNode[] {
    return this.nodes.filter(n => n.floor === this.currentFloor);
  }

  currentFloorEdges(): FloorPlanEdge[] {
    const floorNodeIds = new Set(this.currentFloorNodes().map(n => n.id));
    return this.edges.filter(e => floorNodeIds.has(e.fromNodeId) || floorNodeIds.has(e.toNodeId));
  }

  isEdgeCrossFloor(e: FloorPlanEdge): boolean {
    const from = this.nodeById(e.fromNodeId);
    const to = this.nodeById(e.toNodeId);
    return !!from && !!to && from.floor !== to.floor;
  }

  nodeById(id: string): EditorNode | undefined {
    return this.nodes.find(n => n.id === id);
  }

  nodeColor(n: FloorPlanNode): string {
    return NODE_COLORS[n.nodeType] ?? '#999';
  }

  nodeIcon(n: FloorPlanNode): string {
    switch (n.nodeType) {
      case FloorPlanNodeType.Room:      return 'A';
      case FloorPlanNodeType.Staircase: return 'S';
      case FloorPlanNodeType.Elevator:  return 'E';
      case FloorPlanNodeType.Entrance:  return '⬟';
      case FloorPlanNodeType.Corridor:  return 'C';
    }
  }

  nodeDisplayLabel(n: EditorNode): string {
    if (n.label) return n.label;
    if (n.nodeType === FloorPlanNodeType.Room) {
      const room = this.buildingRooms.find(r => r.id === n.roomId);
      return room?.number ?? '';
    }
    return '';
  }

  hasCrossFloorEdge(n: FloorPlanNode): boolean {
    return this.edges.some(e => {
      if (e.fromNodeId !== n.id && e.toNodeId !== n.id) return false;
      const otherId = e.fromNodeId === n.id ? e.toNodeId : e.fromNodeId;
      const other = this.nodeById(otherId);
      return !!other && other.floor !== n.floor;
    });
  }

  edgeMidX(e: FloorPlanEdge): number {
    return ((this.nodeById(e.fromNodeId)?.x ?? 0) + (this.nodeById(e.toNodeId)?.x ?? 0)) / 2;
  }
  edgeMidY(e: FloorPlanEdge): number {
    return ((this.nodeById(e.fromNodeId)?.y ?? 0) + (this.nodeById(e.toNodeId)?.y ?? 0)) / 2;
  }

  selectedEdge(): FloorPlanEdge | undefined {
    return this.edges.find(e => e.id === this.selectedEdgeId);
  }

  setMode(m: EditorMode): void {
    this.mode = m;
    this.edgeSource = null;
    if (m !== 'select') this.clearSelection();
  }

  clearSelection(): void {
    this.nodes.forEach(n => n.selected = false);
    this.selectedEdgeId = null;
    this.edgeSource = null;
  }

  markDirty(): void { this.dirty = true; }

  onNodeTypeChange(node: EditorNode): void {
    if (node.nodeType !== FloorPlanNodeType.Room) node.roomId = null;
    this.markDirty();
  }

  onRoomAssigned(node: EditorNode, roomId: string | null): void {
    node.roomId = roomId;
    if (roomId) {
      const room = this.buildingRooms.find(r => r.id === roomId);
      if (room && !node.label) node.floor = room.floor;
    }
    this.markDirty();
  }

  onScaleChange(): void { /* scale used on edge creation */ }

  // ── Mouse events ────────────────────────────────────────────────────────────

  onCanvasMouseDown(event: MouseEvent): void {
    if (event.target !== event.currentTarget) return; // hit a node/edge, handled there
    const { x, y } = this.svgPoint(event);

    if (this.mode === 'place') {
      this.placeNode(x, y);
    } else if (this.mode === 'select' || this.mode === 'delete') {
      this.clearSelection();
    }
  }

  onNodeMouseDown(event: MouseEvent, node: EditorNode): void {
    event.stopPropagation();
    const { x, y } = this.svgPoint(event);

    if (this.mode === 'delete') {
      this.deleteNode(node);
      return;
    }
    if (this.mode === 'edge') {
      if (!this.edgeSource) {
        this.edgeSource = { nodeId: node.id };
      } else {
        if (this.edgeSource.nodeId !== node.id) {
          this.createEdge(this.edgeSource.nodeId, node.id);
        }
        this.edgeSource = null;
      }
      return;
    }
    if (this.mode === 'select') {
      this.clearSelection();
      node.selected = true;
      this.selectedEdgeId = null;
      this.dragging = node;
      this.dragOffset = { x: x - node.x, y: y - node.y };
    }
  }

  onEdgeMouseDown(event: MouseEvent, edge: FloorPlanEdge): void {
    event.stopPropagation();
    if (this.mode === 'delete') {
      this.deleteEdge(edge);
      return;
    }
    if (this.mode === 'select') {
      this.clearSelection();
      this.selectedEdgeId = edge.id;
    }
  }

  onMouseMove(event: MouseEvent): void {
    const { x, y } = this.svgPoint(event);
    this.mousePos = { x, y };

    if (this.dragging) {
      const nx = Math.max(NODE_RADIUS, Math.min(CANVAS_W - NODE_RADIUS, x - this.dragOffset.x));
      const ny = Math.max(NODE_RADIUS, Math.min(CANVAS_H - NODE_RADIUS, y - this.dragOffset.y));
      this.dragging.x = nx;
      this.dragging.y = ny;
      this.dirty = true;
    }
  }

  onMouseUp(event: MouseEvent): void {
    this.dragging = null;
  }

  // ── Node / Edge operations ───────────────────────────────────────────────────

  placeNode(x: number, y: number): void {
    const node: EditorNode = {
      id: crypto.randomUUID(),
      buildingId: this.selectedBuildingId!,
      floor: this.currentFloor,
      x, y,
      nodeType: this.placeType,
      roomId: null,
      label: null,
      selected: true
    };
    this.nodes.forEach(n => n.selected = false);
    this.nodes.push(node);
    this.dirty = true;
  }

  createEdge(fromId: string, toId: string): void {
    if (this.edges.some(e =>
      (e.fromNodeId === fromId && e.toNodeId === toId) ||
      (e.fromNodeId === toId && e.toNodeId === fromId))) return;

    const from = this.nodeById(fromId)!;
    const to = this.nodeById(toId)!;
    const dx = from.x - to.x;
    const dy = from.y - to.y;
    const pxDist = Math.sqrt(dx * dx + dy * dy);
    const distMeters = Math.max(1, Math.round(pxDist * this.scale / 100));

    this.edges.push({
      id: crypto.randomUUID(),
      buildingId: this.selectedBuildingId!,
      fromNodeId: fromId,
      toNodeId: toId,
      distanceMeters: distMeters
    });
    this.dirty = true;
  }

  deleteNode(node: EditorNode): void {
    this.edges = this.edges.filter(e => e.fromNodeId !== node.id && e.toNodeId !== node.id);
    this.nodes = this.nodes.filter(n => n.id !== node.id);
    this.dirty = true;
  }

  deleteEdge(edge: FloorPlanEdge): void {
    this.edges = this.edges.filter(e => e.id !== edge.id);
    if (this.selectedEdgeId === edge.id) this.selectedEdgeId = null;
    this.dirty = true;
  }

  // ── Save ─────────────────────────────────────────────────────────────────────

  save(): void {
    if (!this.selectedBuildingId) return;
    this.saving = true;
    const req = {
      nodes: this.nodes.map(n => ({
        id: n.id, floor: n.floor, x: n.x, y: n.y,
        nodeType: n.nodeType, roomId: n.roomId, label: n.label
      })),
      edges: this.edges.map(e => ({
        fromNodeId: e.fromNodeId, toNodeId: e.toNodeId, distanceMeters: e.distanceMeters
      }))
    };
    this.api.saveFloorPlan(this.selectedBuildingId, req).subscribe({
      next: () => {
        this.saving = false;
        this.dirty = false;
        this.snackBar.open('Планировка сохранена', 'OK', { duration: 2000 });
        this.loadFloorPlan(this.selectedBuildingId!);
      },
      error: (e) => {
        this.saving = false;
        this.snackBar.open(e.error?.title || 'Ошибка сохранения', 'OK', { duration: 4000 });
      }
    });
  }

  // ── Helpers ──────────────────────────────────────────────────────────────────

  private svgPoint(event: MouseEvent): { x: number; y: number } {
    const el = this.svgCanvas?.nativeElement;
    if (!el) return { x: event.offsetX, y: event.offsetY };
    const rect = el.getBoundingClientRect();
    return {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top
    };
  }
}
