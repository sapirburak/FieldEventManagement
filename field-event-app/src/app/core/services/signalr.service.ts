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

    // זה ה-State שלנו: "זרם" של אירועים שרכיבים יכולים להירשם אליו
    public eventReceived = new BehaviorSubject<FieldEvent | null>(null);

    constructor(@Inject(PLATFORM_ID) private platformId: Object) {
        // בדיקת הגנה: האם אנחנו רצים בדפדפן?
        if (isPlatformBrowser(this.platformId)) {
            this.initConnection();
        }
    }

    private initConnection(): void {
        // הגדרת החיבור ל-Hub של ה-Backend
        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('https://localhost:7257/eventHub') // כאן יבוא ה-URL של השרת שלך
            .withAutomaticReconnect()
            .build();

        // התחלת החיבור
        this.hubConnection
            .start()
            .then(() => {
                console.log('SignalR Connection started');
                this.hubConnection.invoke('JoinDispatcherGroup'); // הוספת המשתמש לקבוצה
            })
            .catch(err => console.error('Error while starting connection: ', err));

        // האזנה לאירוע ספציפי מהשרת
        this.hubConnection.on('ReceiveNewEvent', (data: FieldEvent) => {
            this.eventReceived.next(data);
        });
    }
}