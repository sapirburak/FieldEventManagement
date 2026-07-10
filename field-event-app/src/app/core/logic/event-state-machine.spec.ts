/// <reference types="jasmine" />
import { EventStateMachine } from './event-state-machine';
import { EventStatus } from '../../data/models/field-event.model';


describe('EventStateMachine', () => {

  it('should allow transition from Unassigned to Assigned', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.Unassigned, EventStatus.Assigned);
    expect(canTransition).toBe(true);
  });

  it('should not allow transition from Unassigned to InProgress (skipping a step)', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.Unassigned, EventStatus.InProgress);
    expect(canTransition).toBe(false);
  });

  it('should allow transition from InProgress to Completed', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.InProgress, EventStatus.Completed);
    expect(canTransition).toBe(true);
  });

  it('should allow transition from Assigned to Assigned (technician transfer)', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.Assigned, EventStatus.Assigned);
    expect(canTransition).toBe(true);
  });

  it('should not allow transition from Completed to any state', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.Completed, EventStatus.Assigned);
    expect(canTransition).toBe(false);
  });

  it('should not allow transition from Cancelled to any state', () => {
    const canTransition = EventStateMachine.canTransition(EventStatus.Cancelled, EventStatus.Assigned);
    expect(canTransition).toBe(false);
  });
});