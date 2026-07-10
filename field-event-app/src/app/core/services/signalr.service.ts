import { Injectable, Inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject } from 'rxjs';
import { FieldEvent } from '../../data/models/field-event.model';

@Injectable({
    providedIn: 'root'
})
export class SignalRService {
    private hubConnection!: signalR.HubConnection;
    private connectionStarted = false;

    public eventReceived = new BehaviorSubject<FieldEvent | null>(null);

    constructor(@Inject(PLATFORM_ID) private platformId: Object) {
        // אין קריאה לחיבור כאן. החיבור נפתח רק לאחר login מוצלח.
        // ראה startConnection() למטה.
    }

    /**
     * נקרא פעם אחת אחרי login מוצלח.
     * accessTokenFactory היא פונקציה שנקראת מחדש בכל reconnect –
     * כך הtoken תמיד עדכני ולא מוקפא מרגע האתחול.
     */
    public startConnection(): void {
        if (!isPlatformBrowser(this.platformId) || this.connectionStarted) {
            return;
        }

        this.connectionStarted = true;

        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('https://localhost:7257/eventHub', {
                // קריאה ל-localStorage בכל פעם מחדש (לא מקפיאים את הtoken).
                // חשוב גם ל-reconnect: אם הtoken התחלף, החיבור יחודש עם הtoken הנכון.
                accessTokenFactory: () => localStorage.getItem('access_token') ?? ''
            })
            .withAutomaticReconnect()
            .build();

        this.hubConnection.on('ReceiveNewEvent', (data: FieldEvent) => {
            this.eventReceived.next(data);
        });

        this.hubConnection
            .start()
            .then(() => {
                console.log('[SignalR] Connection established.');
                return this.hubConnection.invoke('JoinSchedulerGroup');
            })
            .then(() => console.log('[SignalR] Joined Schedulers group.'))
            .catch(err => console.error('[SignalR] Connection failed:', err));
    }
}