import {
  Component, OnInit, OnDestroy, AfterViewInit,
  ViewChild, ElementRef, HostListener, NgZone
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ApiService } from '../../../core/services/api.service';
import { FloorPlanDraftsDialogComponent } from './floor-plan-drafts-dialog.component';
import {
  Building, Room, CreateRoomDto,
  FloorPlanNode, FloorPlanEdge, FloorPlanNodeType,
  RoomType
} from '../../../core/models';

type EditorMode = 'select' | 'place' | 'edge' | 'delete';
interface EditorNode extends FloorPlanNode { selected: boolean; }

const NODE_RADIUS = 24;
const SNAP_PX    = 8;

const NODE_COLORS: Record<string, string> = {
  [FloorPlanNodeType.Room]:      '#1565c0',
  [FloorPlanNodeType.Staircase]: '#e65100',
  [FloorPlanNodeType.Elevator]:  '#6a1b9a',
  [FloorPlanNodeType.Entrance]:  '#2e7d32',
  [FloorPlanNodeType.Corridor]:  '#757575',
};
const NODE_ICONS: Record<string, string> = {
  [FloorPlanNodeType.Room]:      'A',
  [FloorPlanNodeType.Staircase]: 'S',
  [FloorPlanNodeType.Elevator]:  'E',
  [FloorPlanNodeType.Entrance]:  '⬟',
  [FloorPlanNodeType.Corridor]:  'C',
};

@Component({
  selector: 'app-floor-plan-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatButtonModule, MatIconModule, MatSelectModule,
    MatFormFieldModule, MatInputModule, MatSnackBarModule, MatTooltipModule,
    MatProgressSpinnerModule, MatDividerModule, MatDialogModule,
  ],
  template: `
    <div class="editor-page">
      <div class="page-header">
        <h1>Редактор планировок<span class="draft-badge" *ngIf="dirty"> ● черновик</span></h1>
        <div class="header-controls">
          <mat-form-field appearance="outline" class="building-select">
            <mat-label>Корпус</mat-label>
            <mat-select name="bld" [(ngModel)]="selectedBuildingId" (ngModelChange)="onBuildingChange($event)">
              <mat-option *ngFor="let b of buildings" [value]="b.id">{{ b.shortCode }} — {{ b.address }}</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" class="scale-field">
            <mat-label>Масштаб (м/100px)</mat-label>
            <input matInput name="scale" type="number" [(ngModel)]="scale" min="0.1" step="0.5"
                   (ngModelChange)="onScaleChange()">
          </mat-form-field>
        </div>
      </div>

      <div class="editor-container" *ngIf="selectedBuilding; else noBuilding">
        <div class="floor-tabs">
          <button *ngFor="let f of floors" class="floor-tab"
                  [class.active]="f === currentFloor" (click)="selectFloor(f)">
            {{ floorLabel(f) }}
          </button>
        </div>

        <div class="toolbar">
          <div class="btn-group">
            <button mat-stroked-button [class.active-tool]="mode==='select'" (click)="setMode('select')"
                    matTooltip="Выбор / перемещение  [Esc]">
              <mat-icon>mouse</mat-icon>
            </button>
            <button mat-stroked-button [class.active-tool]="mode==='edge'" (click)="setMode('edge')"
                    matTooltip="Нарисовать путь">
              <mat-icon>timeline</mat-icon>
            </button>
            <button mat-stroked-button [class.active-tool]="mode==='delete'" (click)="setMode('delete')"
                    matTooltip="Удалить  [Del]">
              <mat-icon>delete</mat-icon>
            </button>
          </div>
          <mat-divider [vertical]="true" class="v-divider"></mat-divider>
          <div class="btn-group place-types">
            <button *ngFor="let nt of nodeTypes" mat-stroked-button
                    [class.active-tool]="mode==='place' && placeType===nt.type"
                    (click)="setPlaceType(nt.type)"
                    [style.border-color]="nt.color"
                    [matTooltip]="nt.label">
              <span [style.color]="nt.color">{{ nt.label }}</span>
            </button>
          </div>
          <mat-divider [vertical]="true" class="v-divider"></mat-divider>
          <div class="btn-group">
            <button mat-icon-button (click)="zoomBtn(1)" matTooltip="Увеличить  [Ctrl+↑ колесо]">
              <mat-icon>zoom_in</mat-icon>
            </button>
            <button mat-icon-button (click)="zoomBtn(-1)" matTooltip="Уменьшить  [Ctrl+↓ колесо]">
              <mat-icon>zoom_out</mat-icon>
            </button>
            <button mat-icon-button (click)="resetView()" matTooltip="Сбросить вид">
              <mat-icon>fit_screen</mat-icon>
            </button>
          </div>
          <span class="edge-hint" *ngIf="mode==='edge' && edgeSource">
        <mat-icon>arrow_forward</mat-icon> нажмите второй узел
      </span>
          <span class="spacer"></span>
          <button mat-stroked-button (click)="openDraftsDialog()" [disabled]="!selectedBuildingId"
                  matTooltip="Версии и черновики">
            <mat-icon>history</mat-icon>
            Версии
          </button>
          <button mat-button (click)="reload()" [disabled]="loading"
                  matTooltip="Сбросить мой черновик, перезагрузить активную версию">
            <mat-icon>refresh</mat-icon>
          </button>
          <button mat-raised-button color="primary" (click)="save()" [disabled]="saving || !selectedBuildingId">
            <mat-icon>save</mat-icon>
            {{ saving ? 'Сохранение…' : 'Сохранить' }}
          </button>
        </div>

        <div class="main-area">
          <div class="canvas-wrapper" #canvasWrapper
               (mousemove)="onMouseMove($event)"
               (mouseup)="onMouseUp($event)"
               (mouseleave)="onMouseLeave($event)">
            <svg #svgCanvas width="100%" height="100%"
                 class="floor-canvas" preserveAspectRatio="none"
                 [attr.viewBox]="viewBox"
                 [style.cursor]="cursor"
                 (mousedown)="onCanvasMouseDown($event)">
              <defs>
                <pattern id="fp-grid" width="50" height="50" patternUnits="userSpaceOnUse">
                  <path d="M50 0L0 0 0 50" fill="none" stroke="#e8e8e8" stroke-width="1"/>
                </pattern>
              </defs>
              <rect x="-99999" y="-99999" width="199998" height="199998" fill="url(#fp-grid)" pointer-events="none"/>

              <line *ngIf="snapGuideX !== null"
                    [attr.x1]="snapGuideX" y1="-99999" [attr.x2]="snapGuideX" y2="99999"
                    class="snap-guide"/>
              <line *ngIf="snapGuideY !== null"
                    x1="-99999" [attr.y1]="snapGuideY" x2="99999" [attr.y2]="snapGuideY"
                    class="snap-guide"/>

              <!-- Edges -->
              <g *ngFor="let e of currentFloorEdges()">
                <line [attr.x1]="nodeById(e.fromNodeId)?.x??0" [attr.y1]="nodeById(e.fromNodeId)?.y??0"
                      [attr.x2]="nodeById(e.toNodeId)?.x??0" [attr.y2]="nodeById(e.toNodeId)?.y??0"
                      class="edge-line"
                      [class.cross-floor]="isEdgeCrossFloor(e)"
                      [class.sel-edge]="selectedEdgeId===e.id"
                      [attr.stroke-width]="edgeStrokeWidth(e)"
                      (mousedown)="onEdgeMouseDown($event,e)"/>
                <text [attr.x]="edgeMidX(e)" [attr.y]="edgeMidY(e)-8"
                      class="edge-lbl" text-anchor="middle"
                      [attr.font-size]="edgeLabelSize(e)">{{ e.distanceMeters }}м
                </text>
              </g>

              <!-- Edge preview -->
              <line *ngIf="edgeSource && mousePos"
                    [attr.x1]="nodeById(edgeSource.nodeId)?.x??0" [attr.y1]="nodeById(edgeSource.nodeId)?.y??0"
                    [attr.x2]="mousePos.x" [attr.y2]="mousePos.y"
                    class="edge-preview"/>

              <!-- Nodes -->
              <g *ngFor="let node of currentFloorNodes()"
                 [attr.transform]="'translate('+node.x+','+node.y+')'"
                 class="node-g"
                 [class.sel-node]="node.selected"
                 (mousedown)="onNodeMouseDown($event,node)">
                <circle [attr.r]="NR"
                        [attr.fill]="nodeColor(node)"
                        [attr.stroke]="node.selected ? '#ff6f00' : (isUnlinkedRoom(node) ? '#f44336' : '#ffffffcc')"
                        stroke-width="3"/>
                <text text-anchor="middle" dy="0.38em" class="nicon">{{ nodeIcon(node) }}</text>
                <text text-anchor="middle" [attr.dy]="NR + 14" class="nlbl">{{ nodeDisplayLabel(node) }}</text>
                <circle *ngIf="hasCrossFloorEdge(node)" cx="18" cy="-18" r="8" fill="#ff6f00" stroke="#fff"
                        stroke-width="2"/>
                <text *ngIf="hasCrossFloorEdge(node)" x="18" y="-14" text-anchor="middle" font-size="10" fill="#fff"
                      font-weight="bold">↕
                </text>
              </g>
            </svg>
          </div>

          <!-- Props panel -->
          <div class="props-panel">
            <ng-container *ngIf="selectedNodes?.length === 1 && selectedNode as node">
              <div class="panel-title">Узел</div>

              <div class="prop-row">
                <span class="prop-lbl">Тип</span>
                <mat-select name="ntype" [(ngModel)]="node.nodeType" (ngModelChange)="onNodeTypeChange(node)"
                            class="prop-sel">
                  <mat-option *ngFor="let nt of nodeTypes" [value]="nt.type">
                    <span [style.color]="nt.color">{{ nt.label }}</span>
                  </mat-option>
                </mat-select>
              </div>

              <div class="prop-row">
                <span class="prop-lbl">Этаж</span>
                <input name="nfloor" type="number" [(ngModel)]="node.floor" class="prop-input" (change)="markDirty()">
              </div>

              <div class="prop-row" *ngIf="node.nodeType===FNT.Room">
                <span class="prop-lbl">Аудитория (этаж {{ node.floor }})</span>
                <mat-select name="nroom" [(ngModel)]="node.roomId"
                            (ngModelChange)="onRoomAssigned(node,$event)" class="prop-sel">
                  <mat-option [value]="null">— без привязки —</mat-option>
                  <mat-option *ngFor="let r of floorRooms(node)" [value]="r.id">
                    {{ r.number }}
                  </mat-option>
                  <mat-option value="__new__">
                    <span class="add-room-opt"><mat-icon>add_circle_outline</mat-icon> Добавить аудиторию…</span>
                  </mat-option>
                </mat-select>
                <span class="warn" *ngIf="isUnlinkedRoom(node)">⚠ нет привязки/метки</span>
              </div>

              <!-- Inline add-room form -->
              <div class="add-room-form" *ngIf="showAddRoomForm && node.nodeType===FNT.Room">
                <div class="prop-lbl" style="margin-bottom:4px">Новая аудитория</div>
                <div class="add-room-row">
                  <input name="newNum" type="text" [(ngModel)]="newRoomNumber"
                         class="prop-input" placeholder="Номер (обязательно)" style="flex:2">
                  <input name="newCap" type="number" [(ngModel)]="newRoomCapacity"
                         class="prop-input" placeholder="Мест" min="1" style="flex:1">
                </div>
                <div class="add-room-row" style="margin-top:4px">
                  <input type="number" [value]="node.floor" disabled
                         class="prop-input prop-input-disabled" style="flex:1" title="Этаж (из позиции узла)">
                  <button mat-stroked-button color="primary"
                          [disabled]="addingRoom || !newRoomNumber.trim()"
                          (click)="addRoom(node)" style="flex:2;font-size:12px">
                    <mat-icon>add</mat-icon>
                    Создать
                  </button>
                  <button mat-icon-button (click)="showAddRoomForm=false" style="flex-shrink:0">
                    <mat-icon>close</mat-icon>
                  </button>
                </div>
              </div>

              <div class="prop-row">
                <span class="prop-lbl">Метка</span>
                <input name="nlabel" type="text" [(ngModel)]="node.label" class="prop-input"
                       placeholder="Необязательно" (input)="markDirty()">
              </div>

              <div class="prop-row">
                <span class="prop-lbl">X / Y</span>
                <span class="prop-val">{{ node.x|number:'1.0-0' }} / {{ node.y|number:'1.0-0' }}</span>
              </div>

              <!-- Entrance → other campuses -->
              <ng-container *ngIf="node.nodeType===FNT.Entrance">
                <mat-divider style="margin:10px 0 8px"></mat-divider>
                <div class="prop-lbl">Расстояние до корпусов (м)</div>
                <div class="conn-hint">Пусто = нет прохода от этого входа</div>
                <div class="conn-list" *ngIf="otherBuildings.length > 0; else noOtherBld">
                  <div class="conn-row" *ngFor="let b of otherBuildings">
                    <span class="conn-code">{{ b.shortCode }}</span>
                    <input type="number" min="1" step="10" class="prop-input conn-input"
                           [ngModel]="getConnMeters(node, b.id)" [ngModelOptions]="{standalone:true}"
                           (ngModelChange)="setConnMeters(node, b.id, $event)"
                           placeholder="—">
                  </div>
                </div>
                <ng-template #noOtherBld>
                  <div class="conn-hint">Нет других корпусов</div>
                </ng-template>
              </ng-container>

              <!-- Multi-floor stairs/elevator -->
              <ng-container *ngIf="isMultiFloorType(node)">
                <mat-divider style="margin:10px 0 8px"></mat-divider>
                <div class="prop-lbl">Присутствует на этажах</div>
                <div class="floor-checks">
                  <label *ngFor="let f of floors" class="fcheck">
                    <input type="checkbox"
                           [checked]="isStairOnFloor(node,f)"
                           [disabled]="node.floor===f"
                           (change)="toggleStairFloor(node,f,$any($event.target).checked)">
                    <span>{{ floorLabel(f) }}</span>
                  </label>
                </div>
                <button mat-stroked-button class="full-btn" (click)="extendToAllFloors(node)">
                  <mat-icon>layers</mat-icon>
                  Добавить на все этажи
                </button>
              </ng-container>

              <button mat-stroked-button color="warn" class="full-btn" style="margin-top:12px"
                      (click)="deleteNode(node)">
                <mat-icon>delete</mat-icon>
                Удалить узел
              </button>
            </ng-container>

            <ng-container *ngIf="selectedEdgeId && !selectedNode">
              <div class="panel-title">Путь</div>
              <ng-container *ngIf="selectedEdge() as edge">
                <div class="prop-row">
                  <span class="prop-lbl">Расстояние (м)</span>
                  <input name="edist" type="number" [(ngModel)]="edge.distanceMeters"
                         class="prop-input" min="1" (change)="markDirty()">
                </div>
                <div class="prop-row">
                  <span class="prop-lbl">Этажи</span>
                  <span class="prop-val">
                {{ nodeById(edge.fromNodeId)?.floor }} → {{ nodeById(edge.toNodeId)?.floor }}
              </span>
                </div>
                <button mat-stroked-button color="warn" class="full-btn" style="margin-top:10px"
                        (click)="deleteEdge(edge)">
                  <mat-icon>delete</mat-icon>
                  Удалить путь
                </button>
              </ng-container>
            </ng-container>

            <div class="hint-area" *ngIf="selectedNodes as nodes">
              <button mat-stroked-button *ngIf="nodes.length > 1" color="warn" class="full-btn" style="margin-top:12px" (click)="deleteSelectedNodes()">
                <mat-icon>delete</mat-icon>
                Удалить выделенные узлы
              </button>
            </div>

              <div class="hint-area" *ngIf="!selectedNode && !selectedEdgeId">
                <div class="hint-row">
                  <mat-icon class="hint-icon">info</mat-icon>
                  <div class="hint-texts">
                    <p *ngIf="mode==='select'">Нажмите для выбора. Перетащите для перемещения.<br>Узлы прилипают к одной
                      оси.<br>Alt+Drag или колесо — панорама.</p>
                    <p *ngIf="mode==='place'">Нажмите на холст — добавится <b>{{ placeTypeLabel }}</b>.</p>
                    <p *ngIf="mode==='edge'">Нажмите первый узел, затем второй. Esc — отмена.</p>
                    <p *ngIf="mode==='delete'">Нажмите узел или путь для удаления.</p>
                  </div>
                </div>
                <mat-divider style="margin:8px 0"></mat-divider>
                <div class="legend">
                  <div *ngFor="let nt of nodeTypes" class="legend-item">
                    <span class="ldot" [style.background]="nt.color"></span>{{ nt.label }}
                  </div>
                </div>
                <mat-divider style="margin:8px 0"></mat-divider>
                <div class="shortcuts">
                  <div><kbd>Del</kbd> удалить выбранное</div>
                  <div><kbd>Esc</kbd> отмена / выбор</div>
                  <div><kbd>Ctrl+S</kbd> сохранить</div>
                  <div><kbd>Ctrl+Колесо</kbd> масштаб</div>
                  <div><kbd>Alt+Drag</kbd> панорама</div>
                </div>
              </div>
            </div>
          </div>

          <div class="loading-overlay" *ngIf="loading">
            <mat-spinner diameter="48"></mat-spinner>
          </div>
        </div>

        <ng-template #noBuilding>
          <div class="no-bld">
            <mat-icon>apartment</mat-icon>
            <p>Выберите корпус для редактирования планировки</p>
          </div>
        </ng-template>
      </div>
  `,
  styles: [`
    :host { display: block; }

    .editor-page {
      height: calc(100vh - 112px);
      display: flex; flex-direction: column; overflow: hidden;
    }
    .page-header {
      display: flex; align-items: center; justify-content: space-between;
      flex-shrink: 0; margin-bottom: 10px;
    }
    .page-header h1 { margin: 0; font-size: 20px; display: flex; align-items: center; gap: 8px; }
    .draft-badge { font-size: 13px; color: #e65100; font-weight: 400; }
    .header-controls { display: flex; gap: 12px; align-items: center; }
    .building-select { min-width: 260px; }
    .scale-field { width: 145px; }

    .floor-tabs { display: flex; gap: 4px; flex-wrap: wrap; flex-shrink: 0; margin-bottom: 6px; }
    .floor-tab {
      padding: 3px 12px; border: 1px solid #bbb; border-radius: 16px;
      background: #fff; cursor: pointer; font-size: 12px; transition: all 0.12s;
    }
    .floor-tab:hover { background: #e3f2fd; border-color: #1565c0; }
    .floor-tab.active { background: #1565c0; color: #fff; border-color: #1565c0; font-weight: 600; }

    .toolbar {
      display: flex; align-items: center; gap: 8px; flex-shrink: 0;
      flex-wrap: wrap; padding: 4px 0 6px;
    }
    .btn-group { display: flex; gap: 3px; }
    .place-types button { font-size: 12px; }
    .v-divider { height: 30px !important; margin: 0 2px; }
    .active-tool { background: #e3f2fd !important; border-color: #1565c0 !important; }
    .edge-hint { display: flex; align-items: center; gap: 4px; font-size: 12px; color: #e65100; font-weight: 600; }
    .spacer { flex: 1; }

    .editor-container { flex: 1; display: flex; flex-direction: column; overflow: hidden; min-height: 0; position: relative; }
    .main-area { flex: 1; display: flex; gap: 12px; overflow: hidden; min-height: 0; }

    .canvas-wrapper { flex: 1; overflow: hidden; position: relative; min-width: 0; }
    .floor-canvas { width: 100%; height: 100%; display: block; background: #fafafa; border: 1px solid #ddd; border-radius: 4px; user-select: none; }

    /* Snap guides */
    .snap-guide { stroke: #00bcd4; stroke-width: 1; stroke-dasharray: 6 3; pointer-events: none; opacity: 0.75; }

    /* Edges */
    .edge-line { stroke: #78909c; cursor: pointer; fill: none; }
    .edge-line:hover { stroke: #37474f; }
    .edge-line.sel-edge { stroke: #ff6f00; }
    .edge-line.cross-floor { stroke-dasharray: 7 4; stroke: #7986cb; }
    .edge-preview { stroke: #ff8a65; stroke-width: 2; stroke-dasharray: 5 3; pointer-events: none; fill: none; }
    .edge-lbl {
      fill: #37474f; pointer-events: none; font-weight: 700;
      paint-order: stroke fill; stroke: #fff; stroke-width: 4px; stroke-linejoin: round;
    }

    /* Nodes */
    .node-g { cursor: pointer; }
    .node-g:hover circle { filter: brightness(1.12); }
    .nicon {
      font-size: 15px; fill: #fff; pointer-events: none;
      font-family: monospace; font-weight: 900;
      paint-order: stroke fill; stroke: rgba(0,0,0,0.25); stroke-width: 1px;
    }
    .nlbl {
      font-size: 13px; fill: #1a237e; pointer-events: none; font-weight: 700;
      paint-order: stroke fill; stroke: #fff; stroke-width: 4px; stroke-linejoin: round;
    }
    .sel-node circle { filter: drop-shadow(0 0 6px #ff6f00); }

    /* Props panel */
    .props-panel { width: 230px; flex-shrink: 0; overflow-y: auto; display: flex; flex-direction: column; }
    .panel-title { font-size: 14px; font-weight: 700; margin-bottom: 12px; }
    .prop-row { display: flex; flex-direction: column; margin-bottom: 10px; gap: 3px; }
    .prop-lbl { font-size: 10px; color: #555; font-weight: 700; text-transform: uppercase; letter-spacing: 0.4px; }
    .prop-val { font-size: 12px; color: #333; }
    .prop-input { border: 1px solid #ccc; border-radius: 4px; padding: 5px 7px; font-size: 13px; width: 100%; box-sizing: border-box; }
    .prop-input-disabled { background: #f5f5f5; color: #888; cursor: not-allowed; }
    .prop-sel { font-size: 13px; }
    .warn { font-size: 11px; color: #f44336; margin-top: 2px; }
    .full-btn { width: 100%; }
    .add-room-opt { display: flex; align-items: center; gap: 4px; color: #1565c0; font-size: 13px; }
    .add-room-opt mat-icon { font-size: 16px; height: 16px; width: 16px; }
    .add-room-form { background: #e8f5e9; border-radius: 6px; padding: 8px; margin-bottom: 10px; }
    .add-room-row { display: flex; gap: 6px; align-items: center; }

    .conn-hint { font-size: 10px; color: #888; margin: 2px 0 6px; }
    .conn-list { display: flex; flex-direction: column; gap: 5px; }
    .conn-row { display: flex; align-items: center; gap: 8px; }
    .conn-code { font-weight: 700; font-size: 12px; min-width: 28px; color: #2e7d32; }
    .conn-input { flex: 1; }
    .floor-checks { display: flex; flex-wrap: wrap; gap: 4px; margin: 4px 0 6px; }
    .fcheck { display: flex; align-items: center; gap: 3px; font-size: 11px; cursor: pointer; }
    .fcheck input { cursor: pointer; }

    .hint-area { display: flex; flex-direction: column; }
    .hint-row { display: flex; gap: 8px; align-items: flex-start; }
    .hint-icon { color: #afb42b; font-size: 18px; flex-shrink: 0; margin-top: 2px; }
    .hint-texts p { margin: 0; font-size: 12px; color: #555; line-height: 1.55; }
    .legend { display: flex; flex-direction: column; gap: 4px; }
    .legend-item { display: flex; align-items: center; gap: 6px; font-size: 12px; }
    .ldot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
    .shortcuts { display: flex; flex-direction: column; gap: 3px; }
    .shortcuts div { font-size: 11px; color: #666; }
    kbd { background: #f0f0f0; border: 1px solid #ccc; border-radius: 3px; padding: 1px 4px; font-size: 10px; font-family: monospace; }

    .loading-overlay { position: absolute; inset: 0; background: rgba(255,255,255,0.75); display: flex; align-items: center; justify-content: center; z-index: 10; }
    .no-bld { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 16px; padding: 64px; color: #9e9e9e; font-size: 16px; }
    .no-bld mat-icon { font-size: 64px; height: 64px; width: 64px; }
  `]
})
export class FloorPlanEditorComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('svgCanvas', {static: false, read: ElementRef})     svgCanvas!:     ElementRef<SVGSVGElement>;
  @ViewChild('canvasWrapper', {static: false, read: ElementRef}) canvasWrapper!: ElementRef<HTMLDivElement>;

  buildings:          Building[] = [];
  selectedBuildingId: string | null = null;
  selectedBuilding:   Building | null = null;
  buildingRooms:      Room[] = [];

  nodes:        EditorNode[] = [];
  edges:        FloorPlanEdge[] = [];
  floors:       number[] = [];
  currentFloor = 1;
  scale        = 5;
  dirty        = false;
  loading      = false;
  saving       = false;

  mode:       EditorMode        = 'select';
  placeType:  FloorPlanNodeType = FloorPlanNodeType.Room;
  edgeSource: { nodeId: string } | null = null;
  selectedEdgeId: string | null = null;
  mousePos:   { x: number; y: number } | null = null;

  // Snap guide world-coords (null = not snapping)
  snapGuideX: number | null = null;
  snapGuideY: number | null = null;

  // Add-room inline form
  showAddRoomForm = false;
  newRoomNumber   = '';
  newRoomCapacity = 30;
  addingRoom      = false;

  // Pan/zoom — vx,vy = world-space top-left; zoomLevel = screen-pixels per world-unit
  vx         = 0;
  vy         = 0;
  zoomLevel  = 1;
  containerW = 900;
  containerH = 650;

  private dragging:   EditorNode | null = null;
  private dragOffset = { x: 0, y: 0 };
  private dragMoved  = false;
  private panning    = false;
  private panAnchor  = { sx: 0, sy: 0, vx: 0, vy: 0 };
  private resizeObserver!: ResizeObserver;

  private wheelHandler = (e: WheelEvent) => {
    e.preventDefault();
    const r = this.svgCanvas?.nativeElement?.getBoundingClientRect();
    if (!r || r.width === 0) return;
    if (e.ctrlKey || e.metaKey) {
      const factor = e.deltaY > 0 ? 0.87 : 1.15;
      const sx = e.clientX - r.left, sy = e.clientY - r.top;
      const wx = this.vx + (sx / r.width)  * (this.containerW / this.zoomLevel);
      const wy = this.vy + (sy / r.height) * (this.containerH / this.zoomLevel);
      const nz = Math.max(0.1, Math.min(20, this.zoomLevel * factor));
      this.vx = wx - (sx / r.width)  * (this.containerW / nz);
      this.vy = wy - (sy / r.height) * (this.containerH / nz);
      this.NR *= this.zoomLevel / nz;
      this.zoomLevel = nz;
    } else if (e.shiftKey) {
      this.vy += (e.deltaX / r.width)  * (this.containerW / this.zoomLevel);
      this.vx += (e.deltaY / r.height) * (this.containerH / this.zoomLevel);
    } else {
      this.vx += (e.deltaX / r.width)  * (this.containerW / this.zoomLevel);
      this.vy += (e.deltaY / r.height) * (this.containerH / this.zoomLevel);
    }
  };

  NR  = NODE_RADIUS;
  readonly FNT = FloorPlanNodeType;

  readonly nodeTypes = [
    { type: FloorPlanNodeType.Room,      label: 'Аудитория', color: NODE_COLORS[FloorPlanNodeType.Room]      },
    { type: FloorPlanNodeType.Staircase, label: 'Лестница',  color: NODE_COLORS[FloorPlanNodeType.Staircase] },
    { type: FloorPlanNodeType.Elevator,  label: 'Лифт',      color: NODE_COLORS[FloorPlanNodeType.Elevator]  },
    { type: FloorPlanNodeType.Entrance,  label: 'Вход',      color: NODE_COLORS[FloorPlanNodeType.Entrance]  },
    { type: FloorPlanNodeType.Corridor,  label: 'Коридор',   color: NODE_COLORS[FloorPlanNodeType.Corridor]  },
  ];

  get selectedNode():   EditorNode | undefined  { return this.nodes.find(n => n.selected); }
  get selectedNodes():   EditorNode[] { return this.nodes.filter(n => n.selected); }
  get placeTypeLabel(): string                  { return this.nodeTypes.find(t => t.type === this.placeType)?.label ?? ''; }
  get viewBox():        string {
    const vw = this.containerW / this.zoomLevel;
    const vh = this.containerH / this.zoomLevel;
    return `${this.vx} ${this.vy} ${vw} ${vh}`;
  }
  get cursor(): string {
    if (this.panning)          return 'grabbing';
    if (this.mode === 'place') return 'crosshair';
    if (this.mode === 'delete') return 'not-allowed';
    if (this.mode === 'edge')   return 'cell';
    return 'default';
  }

  constructor(private api: ApiService, private snackBar: MatSnackBar, private elementRef: ElementRef, private ngZone: NgZone, private dialog: MatDialog) {}

  openDraftsDialog(): void {
    if (!this.selectedBuilding) return;
    const ref = this.dialog.open(FloorPlanDraftsDialogComponent, {
      data: { buildingId: this.selectedBuilding.id, buildingShortCode: this.selectedBuilding.shortCode },
      width: '760px'
    });
    ref.afterClosed().subscribe(result => {
      if (!result) return;
      if (result.action === 'open-draft' && result.draftId && this.selectedBuilding) {
        // Load the chosen draft into the canvas
        this.api.getFloorPlanDraft(this.selectedBuilding.id, result.draftId).subscribe({
          next: d => {
            try {
              const parsed = JSON.parse(d.draftJson);
              this.nodes = (parsed.nodes ?? []).map((n: any) => ({ ...n, selected: false }));
              this.edges = parsed.edges ?? [];
              this.scale = parsed.scale ?? this.scale;
              this.currentDraftId = d.id;
              this.dirty = true;
              this.snackBar.open(`Загружен черновик «${d.name}»`, '', { duration: 2000 });
            } catch {
              this.snackBar.open('Не удалось разобрать черновик', 'OK', { duration: 4000 });
            }
          },
          error: e => this.snackBar.open(e.error?.title || 'Ошибка', 'OK', { duration: 4000 })
        });
      } else if (result.action === 'load-active' && this.selectedBuildingId) {
        // After publish or activate — reload the canonical floor plan
        this.currentDraftId = null;
        this.loadFloorPlan(this.selectedBuildingId);
      }
    });
  }

  ngOnInit(): void {
    this.api.getBuildings().subscribe(bs => { this.buildings = bs; });
  }

  private mo?: MutationObserver;

  ngAfterViewInit() {
    const root = this.elementRef.nativeElement as HTMLElement;
    const tryAttach = () => {
      const svg = root.querySelector('.floor-canvas') as SVGSVGElement | null;
      if (svg) {
        this.attachWheel(svg);
        return true;
      }
      return false;
    };

    if (!tryAttach()) {
      this.mo = new MutationObserver(() => {
        if (tryAttach() && this.mo) { this.mo.disconnect(); this.mo = undefined; }
      });
      this.mo.observe(root, { childList: true, subtree: true });
    }

    this.resizeObserver = new ResizeObserver(entries => {
      this.ngZone.run(() => {
        const cr = entries[0]?.contentRect;
        if (cr) { this.containerW = cr.width; this.containerH = cr.height; }
      });
    });
  }

  private attachWheel(svg: SVGSVGElement) {
    svg.addEventListener('wheel', this.wheelHandler, { passive: false, capture: true });
  }

  ngOnDestroy(): void {
    this.svgCanvas?.nativeElement?.removeEventListener('wheel', this.wheelHandler);
    this.resizeObserver?.disconnect();
    this.mo?.disconnect();
  }

  @HostListener('window:keydown', ['$event'])
  onKeyDown(e: KeyboardEvent): void {
    const tag = (document.activeElement as HTMLElement)?.tagName ?? '';
    if (['INPUT', 'TEXTAREA'].includes(tag)) return;
    if (e.key === 'Delete' || e.key === 'Backspace') {
      const n = this.selectedNode; if (n) { this.deleteNode(n); return; }
      const ed = this.selectedEdge(); if (ed) this.deleteEdge(ed);
    }
    if (e.key === 'Escape') {
      if (this.edgeSource) { this.edgeSource = null; return; }
      if (this.mode !== 'select') { this.mode = 'select'; return; }
      this.clearSelection();
    }
    if (e.ctrlKey && e.key === 's') { e.preventDefault(); this.save(); }
  }

  @HostListener('window:blur')
  onBlur(): void { this.panning = false; this.dragging = null; this.snapGuideX = null; this.snapGuideY = null; }

  // Building

  onBuildingChange(id: string): void {
    this.selectedBuilding = this.buildings.find(b => b.id === id) ?? null;
    this.buildFloors();
    this.api.getRooms({ buildingId: id }).subscribe(r => { this.buildingRooms = r; });
    this.loadFloorPlan(id);
    setTimeout(() => {
      this.resizeObserver?.disconnect();
      const el = this.canvasWrapper?.nativeElement;
      if (el) {
        this.resizeObserver.observe(el);
        const r = el.getBoundingClientRect();
        if (r.width > 0) this.ngZone.run(() => { this.containerW = r.width; this.containerH = r.height; });
      }
    }, 0);
  }

  buildFloors(): void {
    if (!this.selectedBuilding) { this.floors = []; return; }
    const b = this.selectedBuilding, res: number[] = [];
    for (let f = -b.numberOfBasementFloors; f <= b.numberOfFloors; f++) if (f !== 0) res.push(f);
    this.floors = res;
    this.currentFloor = res.includes(1) ? 1 : (res[0] ?? 1);
  }

  loadFloorPlan(id: string): void {
    this.loading = true; this.nodes = []; this.edges = []; this.clearSelection();
    this.api.getFloorPlan(id).subscribe({
      next: fp => {
        this.nodes = fp.nodes.map(n => ({ ...n, selected: false }));
        this.edges = fp.edges; this.dirty = false; this.loading = false;
        // After loading saved state, check if there is a newer draft
        this.checkDraft(id);
      },
      error: () => { this.loading = false; }
    });
  }

  private currentDraftId: string | null = null;

  private checkDraft(id: string): void {
    this.api.listFloorPlanDrafts(id).subscribe({
      next: drafts => {
        const mine = drafts.find(d => d.isMine);
        if (mine) {
          this.currentDraftId = mine.id;
          this.api.getFloorPlanDraft(id, mine.id).subscribe({
            next: full => {
              if (full?.draftJson) {
                this.snackBar.open('Обнаружен несохранённый черновик', 'Восстановить', { duration: 8000 })
                  .onAction().subscribe(() => this.restoreDraft(id, full.draftJson));
              }
            },
            error: () => {}
          });
        }
      },
      error: () => {}
    });
  }

  reload(): void {
    if (!this.selectedBuildingId) return;
    if (this.currentDraftId) {
      this.api.deleteFloorPlanDraft(this.selectedBuildingId, this.currentDraftId).subscribe({ error: () => {} });
      this.currentDraftId = null;
    }
    this.loadFloorPlan(this.selectedBuildingId);
  }

  restoreDraft(id: string, draftJson: string): void {
    try {
      const d = JSON.parse(draftJson);
      this.nodes = (d.nodes as EditorNode[]).map(n => ({ ...n, selected: false }));
      this.edges = d.edges; this.scale = d.scale ?? 5; this.dirty = true;
      this.snackBar.open('Черновик восстановлен', '', { duration: 2000 });
    } catch { /* ignore bad draft */ }
  }

  private saveDraft(): void {
    const buildingId = this.selectedBuildingId;
    if (!buildingId) return;
    const draftJson = JSON.stringify({ nodes: this.nodes, edges: this.edges, scale: this.scale });

    if (this.currentDraftId) {
      this.api.saveFloorPlanDraft(buildingId, this.currentDraftId, draftJson).subscribe({ error: () => {} });
      return;
    }

    this.api.createFloorPlanDraft(buildingId, `Черновик ${new Date().toLocaleDateString('ru-RU')}`, draftJson).subscribe({
      next: r => { this.currentDraftId = r.id; },
      error: () => {}
    });
  }

  // Floors / view

  selectFloor(f: number): void { this.currentFloor = f; this.clearSelection(); }
  floorLabel(f: number): string { return f < 0 ? `Подвал ${Math.abs(f)}` : `Этаж ${f}`; }

  currentFloorNodes(): EditorNode[] { return this.nodes.filter(n => n.floor === this.currentFloor); }
  currentFloorEdges(): FloorPlanEdge[] {
    const ids = new Set(this.currentFloorNodes().map(n => n.id));
    return this.edges.filter(e => ids.has(e.fromNodeId) || ids.has(e.toNodeId));
  }

  zoomBtn(dir: 1 | -1): void {
    const factor = dir > 0 ? 1.25 : 0.8;
    const cx = this.vx + (this.containerW / this.zoomLevel) / 2;
    const cy = this.vy + (this.containerH / this.zoomLevel) / 2;
    const nz = Math.max(0.1, Math.min(20, this.zoomLevel * factor));
    this.vx = cx - (this.containerW / nz) / 2;
    this.vy = cy - (this.containerH / nz) / 2;
    this.NR *= this.zoomLevel / nz;
    this.zoomLevel = nz;
  }

  resetView(): void { this.vx = 0; this.vy = 0; this.zoomLevel = 1; this.NR = NODE_RADIUS; }

  // Node helpers

  nodeById(id: string): EditorNode | undefined { return this.nodes.find(n => n.id === id); }
  nodeColor(n: FloorPlanNode): string  { return NODE_COLORS[n.nodeType] ?? '#999'; }
  nodeIcon(n: FloorPlanNode): string   { return NODE_ICONS[n.nodeType]  ?? '?'; }

  nodeDisplayLabel(n: EditorNode): string {
    if (n.label) return n.label;
    if (n.nodeType === FloorPlanNodeType.Room)
      return this.buildingRooms.find(r => r.id === n.roomId)?.number ?? '';
    return '';
  }

  isUnlinkedRoom(n: EditorNode): boolean {
    return n.nodeType === FloorPlanNodeType.Room && !n.roomId && !n.label;
  }

  isEdgeCrossFloor(e: FloorPlanEdge): boolean {
    const a = this.nodeById(e.fromNodeId), b = this.nodeById(e.toNodeId);
    return !!a && !!b && a.floor !== b.floor;
  }

  hasCrossFloorEdge(n: FloorPlanNode): boolean {
    return this.edges.some(e => {
      if (e.fromNodeId !== n.id && e.toNodeId !== n.id) return false;
      const other = this.nodeById(e.fromNodeId === n.id ? e.toNodeId : e.fromNodeId);
      return !!other && other.floor !== n.floor;
    });
  }

  edgeMidX(e: FloorPlanEdge): number { return ((this.nodeById(e.fromNodeId)?.x ?? 0) + (this.nodeById(e.toNodeId)?.x ?? 0)) / 2; }
  edgeMidY(e: FloorPlanEdge): number { return ((this.nodeById(e.fromNodeId)?.y ?? 0) + (this.nodeById(e.toNodeId)?.y ?? 0)) / 2; }

  edgeLabelSize(e: FloorPlanEdge): number {
    return Math.max(9, Math.min(16, 9 + Math.sqrt(e.distanceMeters) * 0.55));
  }

  edgeStrokeWidth(e: FloorPlanEdge): number {
    return Math.max(1.5, Math.min(5, 1.5 + e.distanceMeters / 80));
  }

  selectedEdge(): FloorPlanEdge | undefined { return this.edges.find(e => e.id === this.selectedEdgeId); }

  floorRooms(node: EditorNode): Room[] {
    return this.buildingRooms.filter(r => r.floor === node.floor);
  }

  // Entrance to other-building connections

  get otherBuildings(): Building[] {
    return this.buildings.filter(b => b.id !== this.selectedBuildingId);
  }

  getConnMeters(node: EditorNode, toBuildingId: string): number | null {
    return node.connections?.find(c => c.toBuildingId === toBuildingId)?.distanceMeters ?? null;
  }

  setConnMeters(node: EditorNode, toBuildingId: string, value: number | null): void {
    if (!node.connections) node.connections = [];
    const meters = value != null && value > 0 ? Math.round(value) : null;
    const idx = node.connections.findIndex(c => c.toBuildingId === toBuildingId);
    if (meters === null) {
      if (idx >= 0) node.connections.splice(idx, 1); // empty = no connection
    } else if (idx >= 0) {
      node.connections[idx].distanceMeters = meters;
    } else {
      node.connections.push({ toBuildingId, distanceMeters: meters });
    }
    this.markDirty();
  }

  // Mode / selection

  setMode(m: EditorMode): void { this.mode = m; this.edgeSource = null; if (m !== 'select') this.clearSelection(); }
  setPlaceType(t: FloorPlanNodeType): void { this.placeType = t; this.mode = 'place'; }

  clearSelection(): void {
    this.nodes.forEach(n => n.selected = false);
    this.selectedEdgeId = null;
    this.edgeSource     = null;
  }

  markDirty(): void { this.dirty = true; this.saveDraft(); }

  onNodeTypeChange(node: EditorNode): void {
    if (node.nodeType !== FloorPlanNodeType.Room) node.roomId = null;
    this.markDirty();
  }

  onRoomAssigned(node: EditorNode, roomId: string | null): void {
    if (roomId === '__new__') {
      node.roomId = null;
      this.showAddRoomForm = true;
      this.newRoomNumber = '';
      return;
    }
    node.roomId = roomId;
    if (roomId) {
      const r = this.buildingRooms.find(r => r.id === roomId);
      if (r && !node.label) node.floor = r.floor;
    }
    this.markDirty();
  }

  addRoom(node: EditorNode): void {
    if (!this.newRoomNumber.trim() || !this.selectedBuildingId) return;
    this.addingRoom = true;
    const dto: CreateRoomDto = {
      buildingId:       this.selectedBuildingId,
      number:           this.newRoomNumber.trim(),
      roomType:         RoomType.RegularCabinet,
      capacity:         this.newRoomCapacity || 30,
      hasProjector:     false,
      hasComputers:     false,
      hasLab:           false,
      isOnline:         false,
      isEnabled:        true,
      floor:            node.floor,
      allowedLessonTypes: [],
    };
    this.api.createRoom(dto).subscribe({
      next: r => {
        this.buildingRooms.push(r);
        node.roomId = r.id;
        this.showAddRoomForm = false;
        this.newRoomNumber   = '';
        this.addingRoom      = false;
        this.markDirty();
      },
      error: () => { this.addingRoom = false; }
    });
  }

  // Staircase multi-floor

  isMultiFloorType(n: EditorNode): boolean {
    return n.nodeType === FloorPlanNodeType.Staircase || n.nodeType === FloorPlanNodeType.Elevator;
  }

  private stairGroup(ref: EditorNode): EditorNode[] {
    return this.nodes.filter(n =>
      n.nodeType === ref.nodeType && Math.abs(n.x - ref.x) < 15 && Math.abs(n.y - ref.y) < 15
    );
  }

  isStairOnFloor(ref: EditorNode, floor: number): boolean {
    return this.stairGroup(ref).some(n => n.floor === floor);
  }

  toggleStairFloor(ref: EditorNode, floor: number, checked: boolean): void {
    if (checked) {
      if (!this.isStairOnFloor(ref, floor))
        this.nodes.push({ id: crypto.randomUUID(), buildingId: this.selectedBuildingId!, floor, x: ref.x, y: ref.y, nodeType: ref.nodeType, roomId: null, label: ref.label, selected: false });
    } else {
      if (ref.floor === floor) return;
      const v = this.stairGroup(ref).find(n => n.floor === floor && n.id !== ref.id);
      if (v) { this.edges = this.edges.filter(e => e.fromNodeId !== v.id && e.toNodeId !== v.id); this.nodes = this.nodes.filter(n => n.id !== v.id); }
    }
    this.reconnectStairGroup(ref); this.markDirty();
  }

  extendToAllFloors(ref: EditorNode): void {
    const ex = new Set(this.stairGroup(ref).map(n => n.floor));
    for (const f of this.floors)
      if (!ex.has(f))
        this.nodes.push({ id: crypto.randomUUID(), buildingId: this.selectedBuildingId!, floor: f, x: ref.x, y: ref.y, nodeType: ref.nodeType, roomId: null, label: ref.label, selected: false });
    this.reconnectStairGroup(ref); this.markDirty();
  }

  private reconnectStairGroup(ref: EditorNode): void {
    const g = this.stairGroup(ref).sort((a, b) => a.floor - b.floor);
    const gids = new Set(g.map(n => n.id));
    this.edges = this.edges.filter(e => !(gids.has(e.fromNodeId) && gids.has(e.toNodeId)));
    for (let i = 0; i < g.length - 1; i++)
      this.edges.push({ id: crypto.randomUUID(), buildingId: this.selectedBuildingId!, fromNodeId: g[i].id, toNodeId: g[i+1].id, distanceMeters: 15 });
  }

  // Snapping

  private applySnap(node: EditorNode, rx: number, ry: number): { x: number; y: number } {
    const t = SNAP_PX / this.zoomLevel;
    let x = rx, y = ry;
    this.snapGuideX = null; this.snapGuideY = null;
    for (const o of this.nodes) {
      if (o.id === node.id || o.floor !== node.floor) continue;
      if (Math.abs(rx - o.x) < t) { x = o.x; this.snapGuideX = o.x; }
      if (Math.abs(ry - o.y) < t) { y = o.y; this.snapGuideY = o.y; }
    }
    return { x, y };
  }

  // Mouse events

  onCanvasMouseDown(event: MouseEvent): void {
    if (event.button === 1 || event.altKey) { event.preventDefault(); this.startPan(event); return; }
    if (event.target !== event.currentTarget) return;
    const { x, y } = this.svgPoint(event);
    if (this.mode === 'place') { this.placeNode(x, y); }
    else if (this.mode === 'select' && !event.ctrlKey) { this.clearSelection(); this.startPan(event); }
    else if (this.mode === 'delete') { this.clearSelection(); }
  }

  private startPan(event: MouseEvent): void {
    this.panning   = true;
    this.panAnchor = { sx: event.clientX, sy: event.clientY, vx: this.vx, vy: this.vy };
  }

  onNodeMouseDown(event: MouseEvent, node: EditorNode): void {
    event.stopPropagation();
    if (event.button === 1 || event.altKey) { this.startPan(event); return; }
    const { x, y } = this.svgPoint(event);
    if (event.ctrlKey && this.mode === 'select') { node.selected = !node.selected; return; }
    if (this.mode === 'place') {
      if (this.placeType !== node.nodeType) {
        this.placeType = node.nodeType;
        return;
      }
    }
    if (this.mode === 'delete')  { this.deleteNode(node); return; }
    if (this.mode === 'edge') {
      if (!this.edgeSource) { this.edgeSource = { nodeId: node.id }; }
      else { if (this.edgeSource.nodeId !== node.id) this.createEdge(this.edgeSource.nodeId, node.id); this.edgeSource = null; }
      return;
    }
    this.clearSelection();
    node.selected = true; this.selectedEdgeId = null;
    this.dragging = node; this.dragOffset = { x: x - node.x, y: y - node.y }; this.dragMoved = false;
  }

  onEdgeMouseDown(event: MouseEvent, edge: FloorPlanEdge): void {
    event.stopPropagation();
    if (this.mode === 'delete') { this.deleteEdge(edge); return; }
    if (this.mode === 'select') { this.clearSelection(); this.selectedEdgeId = edge.id; }
  }

  onMouseMove(event: MouseEvent): void {
    if (this.panning) {
      const r = this.svgCanvas?.nativeElement?.getBoundingClientRect();
      if (r && r.width > 0) {
        const vw = this.containerW / this.zoomLevel;
        const vh = this.containerH / this.zoomLevel;
        this.vx = this.panAnchor.vx - (event.clientX - this.panAnchor.sx) / r.width  * vw;
        this.vy = this.panAnchor.vy - (event.clientY - this.panAnchor.sy) / r.height * vh;
      }
      return;
    }
    const { x, y } = this.svgPoint(event);
    this.mousePos = { x, y };
    if (this.dragging) {
      const snapped = this.applySnap(this.dragging, x - this.dragOffset.x, y - this.dragOffset.y);
      this.dragging.x = snapped.x; this.dragging.y = snapped.y;
      this.recomputeEdgesFor(this.dragging.id);
      this.dirty = true; this.dragMoved = true;
    }
  }

  //Recompute distanceMeters for every edge that touches `nodeId`, using current scale.
  private recomputeEdgesFor(nodeId: string): void {
    const node = this.nodeById(nodeId);
    if (!node) return;
    for (const edge of this.edges) {
      if (edge.fromNodeId !== nodeId && edge.toNodeId !== nodeId) continue;
      const otherId = edge.fromNodeId === nodeId ? edge.toNodeId : edge.fromNodeId;
      const other = this.nodeById(otherId);
      if (!other) continue;
      const dx = node.x - other.x, dy = node.y - other.y;
      edge.distanceMeters = Math.max(1, Math.round(Math.sqrt(dx*dx + dy*dy) * this.scale / 100));
    }
  }

  //Recompute all edge lengths (used when scale changes).
  recomputeAllEdges(): void {
    for (const edge of this.edges) {
      const a = this.nodeById(edge.fromNodeId);
      const b = this.nodeById(edge.toNodeId);
      if (!a || !b) continue;
      const dx = a.x - b.x, dy = a.y - b.y;
      edge.distanceMeters = Math.max(1, Math.round(Math.sqrt(dx*dx + dy*dy) * this.scale / 100));
    }
    this.markDirty();
  }

  onScaleChange(): void {
    if (!this.scale || this.scale <= 0) return;
    this.recomputeAllEdges();
  }

  onMouseUp(_event: MouseEvent): void {
    if (this.panning) { this.panning = false; return; }
    if (this.dragging && this.dragMoved) this.saveDraft();
    this.dragging = null; this.dragMoved = false;
    this.snapGuideX = null; this.snapGuideY = null;
  }

  onMouseLeave(event: MouseEvent): void { this.onMouseUp(event); this.mousePos = null; }

  // Operations

  placeNode(x: number, y: number): void {
    const node: EditorNode = {
      id: crypto.randomUUID(), buildingId: this.selectedBuildingId!,
      floor: this.currentFloor, x, y,
      nodeType: this.placeType, roomId: null, label: null, selected: true
    };
    this.nodes.forEach(n => n.selected = false);
    this.nodes.push(node); this.markDirty();
  }

  createEdge(fromId: string, toId: string): void {
    if (this.edges.some(e => (e.fromNodeId===fromId&&e.toNodeId===toId)||(e.fromNodeId===toId&&e.toNodeId===fromId))) return;
    const a = this.nodeById(fromId)!, b = this.nodeById(toId)!;
    const dx = a.x - b.x, dy = a.y - b.y;
    this.edges.push({ id: crypto.randomUUID(), buildingId: this.selectedBuildingId!, fromNodeId: fromId, toNodeId: toId, distanceMeters: Math.max(1, Math.round(Math.sqrt(dx*dx+dy*dy) * this.scale / 100)) });
    this.markDirty();
  }

  deleteSelectedNodes(): void {
    for (let node of this.selectedNodes) {
      this.edges = this.edges.filter(e => e.fromNodeId!==node.id && e.toNodeId!==node.id);
      this.nodes = this.nodes.filter(n => n.id!==node.id);
    }
    this.markDirty();
  }

  deleteNode(node: EditorNode): void {
    this.edges = this.edges.filter(e => e.fromNodeId!==node.id && e.toNodeId!==node.id);
    this.nodes = this.nodes.filter(n => n.id!==node.id);
    this.markDirty();
  }

  deleteEdge(edge: FloorPlanEdge): void {
    this.edges = this.edges.filter(e => e.id!==edge.id);
    if (this.selectedEdgeId===edge.id) this.selectedEdgeId=null;
    this.markDirty();
  }

  save(): void {
    if (!this.selectedBuildingId || this.saving) return;
    this.saving = true;
    this.api.saveFloorPlan(this.selectedBuildingId, {
      nodes: this.nodes.map(n => ({
        id: n.id, floor: n.floor, x: n.x, y: n.y, nodeType: n.nodeType, roomId: n.roomId, label: n.label,
        connections: n.nodeType === FloorPlanNodeType.Entrance ? (n.connections ?? []) : []
      })),
      edges: this.edges.map(e => ({ fromNodeId: e.fromNodeId, toNodeId: e.toNodeId, distanceMeters: e.distanceMeters }))
    }).subscribe({
      next: () => {
        this.saving = false; this.dirty = false;
        localStorage.removeItem(`fp_draft_${this.selectedBuildingId}`);
        const u = this.nodes.filter(n => this.isUnlinkedRoom(n)).length;
        this.snackBar.open(u > 0 ? `Сохранено (${u} ауд. без привязки)` : 'Планировка сохранена', 'OK', { duration: 2500 });
        this.loadFloorPlan(this.selectedBuildingId!);
      },
      error: err => { this.saving = false; this.snackBar.open(err.error?.title || 'Ошибка сохранения', 'OK', { duration: 4000 }); }
    });
  }


  private svgPoint(event: MouseEvent): { x: number; y: number } {
    const r = this.svgCanvas?.nativeElement?.getBoundingClientRect();
    if (!r || r.width === 0) return { x: 0, y: 0 };
    return {
      x: this.vx + (event.clientX - r.left) / r.width  * (this.containerW / this.zoomLevel),
      y: this.vy + (event.clientY - r.top)  / r.height * (this.containerH / this.zoomLevel),
    };
  }
}
