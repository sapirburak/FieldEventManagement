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
        // No connection is started here. The connection opens only after a successful login.
        // See startConnection() below.
    }

    /**
     * Called once after a successful login.
     * accessTokenFactory is a function re-invoked on every reconnect –
     * ensuring the token is always fresh and not frozen from initialization time.
     */
    public startConnection(): void {
        if (!isPlatformBrowser(this.platformId) || this.connectionStarted) {
            return;
        }

        this.connectionStarted = true;

        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('https://localhost:7257/eventHub', {
                // Read from localStorage each time (token is never frozen).
                // Important for reconnect: if the token changes, the connection resumes with the correct token.
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