/// <reference types="jasmine" />
import { EventStateMachine } from './event-state-machine';
import { EventStatus } from '../../data/models/field-event.model';


describe('EventStateMachine', () => {
  
  it('should allow tran sition from New to Assigned', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.New, EventStatus.Assigned);
    expect(canTransition).toBe(true);
  });

  it('should not allow transition from New to InProgress (invalid logic)', () => {
    // אי אפשר לדלג מ-New ישר ל-InProgress
    const canTransition = EventStateMachine.canTransition(EventStatus.New, EventStatus.InProgress);
    expect(canTransition).toBe(false);
  });

  it('should allow transition from InProgress to Closed', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.InProgress, EventStatus.Closed);
    expect(canTransition).toBe(true);
  });

  it('should not allow transition from Closed to any state', () => {
    // אי אפשר לשנות מצב לאירוע שנסגר
    const canTransition = EventStateMachine.canTransition(EventStatus.Closed, EventStatus.Assigned);
    expect(canTransition).toBe(false);
  });
});