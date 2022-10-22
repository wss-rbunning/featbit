import { Component, EventEmitter, Input, Output, } from '@angular/core';
import { SegmentService } from '@services/segment.service';
import { isSegmentRule, trackByFunction } from '@utils/index';
import { IRuleOp, ruleOps } from './ruleConfig';
import { ISegment } from "@features/safe/segments/types/segments-index";
import { IUserProp } from "@shared/types";
import {USER_IS_IN_SEGMENT, USER_IS_NOT_IN_SEGMENT} from "@shared/constants";
import {ICondition, IRule, IRuleVariation, IVariation} from "@shared/rules";

@Component({
  selector: 'find-rule',
  templateUrl: './find-rule.component.html',
  styleUrls: ['./find-rule.component.less']
})
export class FindRuleComponent {

  @Input() userProps: IUserProp[] = [];

  @Output() deleteRule = new EventEmitter<string>();
  @Output() updateRuleName = new EventEmitter<string>();
  @Output() onConditionChange = new EventEmitter<ICondition[]>();

  public conditions: ICondition[] = [];
  public name: string = "";
  public id: string = "";
  variations: IRuleVariation[] = [];
  trackByFunction = trackByFunction;

  segmentList: ISegment[] = [];

  constructor(
    private segmentService: SegmentService
  ) { }

  @Input()
  set data(value: IRule) {
    this.id = value.id;
    this.name = value.name;
    this.variations = value.variations || [];
    this.conditions = [];

    if(value.conditions.length === 0) {
      this.conditions.push({
        property: '',
        op: '',
        value: '',
        multipleValue: []
      } as ICondition);
    } else {
      const segmentIds = value.conditions.flatMap((item: ICondition) => {
        const isSegment = isSegmentRule(item);
        let opType: string = isSegment ? 'multi': ruleOps.filter((op: IRuleOp) => op.value === item.op)[0].type;

        let defaultValue: string;
        let multipleValue: string[];

        if(opType === 'multi') {
          multipleValue = JSON.parse(item.value || '[]');
          defaultValue = '';
        } else {
          defaultValue = item.value;
          multipleValue = [];
        }
        this.conditions.push({
          property: item.property,
          op: isSegment ? '': item.op,
          value: defaultValue,
          multipleValue: [...multipleValue],
          type: opType
        } as ICondition);
        return isSegment? [...multipleValue] : [];
      })

      if (segmentIds.length > 0) {
        this.segmentService.getByIds(segmentIds).subscribe((segs: ISegment[]) => {
          this.segmentList = [...segs];
        });
      }
    }
  }

  onAddRule() {
    this.conditions.push({
      property: '',
      op: '',
      value: '',
      multipleValue: []
    } as ICondition)
  }

  onDeleteRule() {
    this.deleteRule.emit(this.id);
  }

  public onDeleteRuleItem(index: number) {
    if(this.conditions.length === 1) {
      this.conditions[0] = {
        property: '',
        op: '',
        value: '',
        multipleValue: []
      } as ICondition
    } else {
      this.conditions.splice(index, 1);
    }
    this.onConditionChange.next(this.conditions);
  }

  public onRuleChange(value: ICondition, index: number) {
    const rule = { ...value, ...{multipleValue: [...value.multipleValue]} };
    if (isSegmentRule(rule)) {
      rule.op = null;
    }

    this.conditions = this.conditions.map((item, idx) => idx === index ? rule : item);
    this.onConditionChange.next(this.conditions);
  }

  public onRuleNameChange() {
    this.updateRuleName.emit(this.name);
  }

  canViewTargetedUsers(): boolean {
    const segmentProperties = [USER_IS_IN_SEGMENT, USER_IS_NOT_IN_SEGMENT];
    const segmentRules = this.conditions.filter(x => segmentProperties.includes(x.property));
    return segmentRules.length === 0;
  }

  targetedUsersModalVisible: boolean = false;
  viewTargetedUsers() {
    this.targetedUsersModalVisible = true;
  }

  @Output() onServeChange = new EventEmitter<IRuleVariation[]>();
  @Input() variationOptions: IVariation[] = [];
}
